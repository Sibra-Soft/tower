using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TowerSpeech.Core;

public sealed class SpeechDocument : FragmentDocument
{
    public SpeechDocument() => SchemaVersion = 2;
    public long Revision
    {
        get; set;
    }
    public CallsignLexicon Callsigns { get; set; } = new();
    public SpeechPreferences Preferences { get; set; } = new();
    public List<SpeechSequence> Sequences { get; set; } = [];
    public List<JsonElement> ReviewHistory { get; set; } = [];
    public string RecoveredReviewHistory { get; set; } = "";
    public Dictionary<string, string> AudioBankHashes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData
    {
        get; set;
    }
}

/// <summary>One authoritative JSON; atomic writes, retained backups and serialized edits.</summary>
public static class SpeechDataStore
{
    public static JsonSerializerOptions Options
    {
        get;
    } = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };
    public static string FilePath(string directory) => Path.Combine(Path.GetFullPath(directory), "Speech.json");

    public static string BackupDirectory(string directory)
    {
        var data = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        var parent = Directory.GetParent(data)
            ?? throw new ArgumentException("The game Data folder must have a parent directory.", nameof(directory));
        var root = Path.GetFileName(data).Equals("Data", StringComparison.OrdinalIgnoreCase) ? parent.FullName : data;
        return Path.Combine(root, "Backups", "Speech");
    }

    public static SpeechDocument Load(string directory)
    {
        return Locked(directory, () => Read(directory));
    }

    private static SpeechDocument Read(string directory)
    {
        // Existing readers keep their complete snapshot while a writer atomically
        // replaces the path; they must not block replacement on Windows.
        using var stream = new FileStream(FilePath(directory), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var document = JsonSerializer.Deserialize<SpeechDocument>(stream, Options)
            ?? throw new InvalidDataException("Speech.json is empty.");
        Validate(document);
        return document;
    }

    public static SpeechDocument Create(string directory, SpeechDocument document)
    {
        return Locked(directory, () =>
        {
            if (File.Exists(FilePath(directory)))
            {
                throw new IOException("Speech.json already exists; an import will not replace it.");
            }
            Validate(document);
            Write(directory, document);
            return document;
        });
    }

    public static SpeechDocument Update(string directory, Action<SpeechDocument> edit)
    {
        return Locked(directory, () =>
        {
            var document = Read(directory);
            edit(document);
            document.Revision++;
            Validate(document);
            Write(directory, document);
            return document;
        });
    }

    internal static T Locked<T>(string directory, Func<T> action)
    {
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(FilePath(directory).ToUpperInvariant())));
        using var mutex = new Mutex(false, "TowerSpeech_" + key);
        var acquired = false;
        try
        {
            try
            {
                acquired = mutex.WaitOne(TimeSpan.FromSeconds(10));
            }
            catch (AbandonedMutexException)
            {
                acquired = true;
            }
            if (!acquired)
            {
                throw new IOException("Speech data is being saved by another process. Please retry.");
            }
            return action();
        }
        finally
        {
            if (acquired)
            {
                mutex.ReleaseMutex();
            }
        }
    }

    private static void Write(string directory, SpeechDocument document)
    {
        Directory.CreateDirectory(directory);
        var path = FilePath(directory);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(document, Options);
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            if (File.Exists(path))
            {
                var backups = BackupDirectory(directory);
                Directory.CreateDirectory(backups);
                var backup = Path.Combine(backups, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + "-" + Guid.NewGuid().ToString("N") + ".bak");
                File.Replace(temporary, path, backup);
            }
            else
            {
                File.Move(temporary, path);
            }
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    internal static void Validate(SpeechDocument document)
    {
        if (document.SchemaVersion != 2 || document.Fragments is null || document.Callsigns is null || document.Preferences is null
            || document.Sequences is null || document.ReviewHistory is null || document.AudioBankHashes is null
            || document.Callsigns.FullCallsigns is null || document.Callsigns.Prefixes is null
            || document.Fragments.Any(f => f is null || f.Bank is < 1 or > 6 || f.Index < 1
                || (f.Bank <= 2 || f.Bank == 6) && f.AirportCode != ""
                || f.Bank is >= 3 and <= 5 && (string.IsNullOrEmpty(f.AirportCode) ||
                    !System.Text.RegularExpressions.Regex.IsMatch(f.AirportCode, @"^[A-Z0-9]{3,8}$"))))
        {
            throw new InvalidDataException("Invalid Speech.json schema. The file has not been replaced.");
        }
        _ = VoiceCatalog.FromFragments(document.Fragments);
        if (!Enum.IsDefined(document.Preferences.Engine) || document.Preferences.Rate is < -10 or > 10
            || document.Preferences.NoisePercent is < 0 or > 30 || document.Preferences.ArrivalDistanceMiles is < 1 or > 99)
        {
            throw new InvalidDataException("Invalid speech preferences.");
        }
    }
}
