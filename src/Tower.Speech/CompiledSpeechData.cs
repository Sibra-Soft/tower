using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace TowerSpeech.Core;

public sealed record SpeechBuildResult(string Path, long SourceRevision, int Fragments, int ApprovedFragments, long Bytes);

/// <summary>Versioned binary runtime catalog. Source reviews/history never enter this file.</summary>
public static class CompiledSpeechData
{
    public const string FileName = "SPEECH.BIN";
    private const int Version = 1;
    private const int MaxBytes = 16 * 1024 * 1024;
    private static readonly Encoding Utf8 = new UTF8Encoding(false, true);
    private sealed record Package(SpeechDocument Document, byte[] SourceHash);

    public static string FilePath(string data) => Path.Combine(Path.GetFullPath(data), FileName);

    public static SpeechBuildResult Build(string data, bool preserveSoundSettings = true, string? sourceDirectory = null)
        => SpeechDataStore.Locked(sourceDirectory ?? data, () => SpeechDataStore.Locked(data, () =>
    {
        var source = sourceDirectory ?? data;
        var document = SpeechDataStore.Load(source);
        var audio = new VocAudioRepository(data);
        audio.ValidateSources(document);
        foreach (var fragment in document.Fragments)
        {
            if (!audio.HasFragment(new(fragment.Bank, fragment.Index, fragment.AirportCode)))
            {
                throw new InvalidDataException($"Cannot build speech: missing audio reference ({fragment.Bank}:{fragment.Index}) [{fragment.AirportCode}].");
            }
        }
        if (preserveSoundSettings && File.Exists(FilePath(data)))
        {
            document.Preferences = Read(data).Document.Preferences.Copy();
        }
        var sourceHash = SHA256.HashData(File.ReadAllBytes(SpeechDataStore.FilePath(source)));
        Write(data, new(document, sourceHash));
        return new SpeechBuildResult(FilePath(data), document.Revision, document.Fragments.Count,
            document.Fragments.Count(f => f.Status == "clear" && f.Confidence == "high"), new FileInfo(FilePath(data)).Length);
    }));

    public static SpeechDocument Load(string data) => SpeechDataStore.Locked(data, () => Read(data).Document);

    public static SpeechDocument LoadRuntime(string data, IEnumerable<string> airports)
    {
        var document = Load(data);
        foreach (var airport in airports)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(airport, @"^[A-Z0-9]{3,8}$"))
            {
                throw new InvalidDataException("Invalid airport speech code.");
            }
            var folder = Path.Combine(data, airport);
            if (!File.Exists(FilePath(folder)))
            {
                continue;
            }
            var local = LoadAirport(folder, airport);
            var identities = local.Fragments.Select(f => new FragmentId(f.Bank, f.Index, airport)).ToHashSet();
            document.Fragments.RemoveAll(f => identities.Contains(new(f.Bank, f.Index, f.AirportCode)));
            document.Fragments.AddRange(local.Fragments);
            foreach (var hash in local.AudioBankHashes)
            {
                document.AudioBankHashes[hash.Key] = hash.Value;
            }
        }
        return document;
    }

    public static SpeechDocument LoadAirport(string folder, string airport)
    {
        var document = Load(folder);
        if (document.Fragments.Any(f => f.Bank is < 3 or > 5 || f.AirportCode != airport) ||
            document.Callsigns.Prefixes.Count != 0 || document.Callsigns.FullCallsigns.Count != 0 ||
            document.Sequences.Count != 0 || document.AudioBankHashes.Keys.Any(key =>
                !new[] { $"{airport}/{airport}F.VOC", $"{airport}/{airport}R.VOC", $"{airport}/{airport}T.VOC" }.Contains(key, StringComparer.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException("Airport speech data may only reference its own VOC banks.");
        }
        return document;
    }

    public static void BuildAirport(string data, string airport, string sourceDirectory)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(airport, @"^[A-Z0-9]{3,8}$"))
        {
            throw new InvalidDataException("Invalid airport speech code.");
        }
        var source = SpeechDataStore.Load(sourceDirectory);
        var local = new SpeechDocument
        {
            Revision = source.Revision,
            Fragments = source.Fragments.Where(f => f.Bank is >= 3 and <= 5 && f.AirportCode == airport).ToList(),
            AudioBankHashes = source.AudioBankHashes.Where(pair => pair.Key.StartsWith(airport + "/", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase)
        };
        var audio = new VocAudioRepository(data);
        audio.ValidateSources(local);
        if (local.Fragments.Any(f => !audio.HasFragment(new(f.Bank, f.Index, airport))))
        {
            throw new InvalidDataException("Airport speech contains a missing audio reference.");
        }
        var folder = Path.Combine(data, airport);
        Directory.CreateDirectory(folder);
        SpeechDataStore.Locked(folder, () =>
        {
            Write(folder, new(local, SHA256.HashData(File.ReadAllBytes(SpeechDataStore.FilePath(sourceDirectory)))),
                Path.Combine(Directory.GetParent(Path.GetFullPath(data))!.FullName, "Backups", "SpeechBuild", airport));
            return true;
        });
    }

    public static bool IsCurrent(string data, string? sourceDirectory = null)
        => SpeechDataStore.Locked(sourceDirectory ?? data, () => SpeechDataStore.Locked(data, () =>
        File.Exists(FilePath(data)) && CryptographicOperations.FixedTimeEquals(Read(data).SourceHash,
            SHA256.HashData(File.ReadAllBytes(SpeechDataStore.FilePath(sourceDirectory ?? data))))));

    public static void SavePreferences(string data, SpeechPreferences preferences)
    {
        var copy = preferences.Copy();
        SpeechDataStore.Locked(data, () =>
        {
            var package = Read(data);
            package.Document.Preferences = copy;
            Write(data, package);
            return true;
        });
    }

    private static void Text(BinaryWriter writer, string value)
    {
        var bytes = Utf8.GetBytes(value);
        if (bytes.Length > 16384)
        {
            throw new InvalidDataException("Speech field is too long.");
        }
        writer.Write((ushort)bytes.Length);
        writer.Write(bytes);
    }

    private static string Text(BinaryReader reader)
    {
        var count = reader.ReadUInt16();
        if (count > 16384)
        {
            throw new InvalidDataException("Invalid speech field length.");
        }
        return Utf8.GetString(Exact(reader, count));
    }

    private static byte[] Exact(BinaryReader reader, int count)
    {
        var bytes = reader.ReadBytes(count);
        return bytes.Length == count ? bytes : throw new InvalidDataException("Truncated compiled speech data.");
    }

    private static int Count(BinaryReader reader)
    {
        var count = reader.ReadInt32();
        return count is >= 0 and <= 20000 ? count : throw new InvalidDataException("Invalid speech collection size.");
    }

    private static void Reference(BinaryWriter writer, int bank, int index, string airport)
    {
        writer.Write(bank);
        writer.Write(index);
        Text(writer, airport);
    }

    private static FragmentId Reference(BinaryReader reader) => new(reader.ReadInt32(), reader.ReadInt32(), Text(reader));

    private static void Aliases(BinaryWriter writer, Dictionary<string, CallsignLexicon.Alias> aliases)
    {
        writer.Write(aliases.Count);
        foreach (var (key, alias) in aliases.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            Text(writer, key);
            Text(writer, alias.Text);
            Reference(writer, alias.Bank, alias.Index, alias.AirportCode);
        }
    }

    private static void Aliases(BinaryReader reader, Dictionary<string, CallsignLexicon.Alias> aliases)
    {
        var count = Count(reader);
        for (var i = 0; i < count; i++)
        {
            var key = Text(reader);
            var text = Text(reader);
            var id = Reference(reader);
            aliases.Add(key, new() { Text = text, Bank = id.Bank, Index = id.Index, AirportCode = id.AirportCode });
        }
    }

    private static byte[] Encode(SpeechDocument document)
    {
        SpeechDataStore.Validate(document);
        using var memory = new MemoryStream();
        using var writer = new BinaryWriter(memory, Utf8, true);
        writer.Write(document.Revision);
        var preferences = document.Preferences;
        writer.Write((int)preferences.Engine);
        Text(writer, preferences.VoiceName);
        writer.Write(preferences.Rate);
        writer.Write(preferences.RadioEffect);
        writer.Write(preferences.NoisePercent);
        writer.Write(preferences.ArrivalDistanceMiles);
        writer.Write(document.Fragments.Count);
        foreach (var fragment in document.Fragments.OrderBy(f => f.Bank).ThenBy(f => f.AirportCode, StringComparer.Ordinal).ThenBy(f => f.Index))
        {
            Reference(writer, fragment.Bank, fragment.Index, fragment.AirportCode);
            Text(writer, fragment.FileName);
            Text(writer, fragment.Transcript ?? "");
            writer.Write(fragment.Status == "clear" && fragment.Confidence == "high");
        }
        Aliases(writer, document.Callsigns.Prefixes);
        Aliases(writer, document.Callsigns.FullCallsigns);
        writer.Write(document.AudioBankHashes.Count);
        foreach (var (key, value) in document.AudioBankHashes.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            Text(writer, key);
            Text(writer, value);
        }
        writer.Write(document.Sequences.Count);
        foreach (var sequence in document.Sequences)
        {
            Text(writer, sequence.Id);
            Text(writer, sequence.Name);
            Text(writer, sequence.AirportCode);
            Text(writer, sequence.IntentKey);
            Text(writer, sequence.Speaker);
            writer.Write(sequence.Steps.Count);
            foreach (var step in sequence.Steps)
            {
                Text(writer, step.Kind);
                Text(writer, step.Text);
                Reference(writer, step.Bank, step.Index, step.AirportCode);
            }
        }
        writer.Flush();
        if (memory.Length > MaxBytes)
        {
            throw new InvalidDataException("Compiled speech data exceeds the size limit.");
        }
        return memory.ToArray();
    }

    private static SpeechDocument Decode(byte[] bytes)
    {
        using var memory = new MemoryStream(bytes, false);
        using var reader = new BinaryReader(memory, Utf8);
        var document = new SpeechDocument { Revision = reader.ReadInt64() };
        document.Preferences = new()
        {
            Engine = (SpeechEngineKind)reader.ReadInt32(), VoiceName = Text(reader), Rate = reader.ReadInt32(),
            RadioEffect = reader.ReadBoolean(), NoisePercent = reader.ReadInt32(), ArrivalDistanceMiles = reader.ReadInt32()
        };
        var fragments = Count(reader);
        for (var i = 0; i < fragments; i++)
        {
            var id = Reference(reader);
            var file = Text(reader);
            var text = Text(reader);
            var approved = reader.ReadBoolean();
            document.Fragments.Add(new()
            {
                Bank = id.Bank, Index = id.Index, AirportCode = id.AirportCode, FileName = file, Transcript = text,
                Expression = id.ToString(), Status = approved ? "clear" : "uncertain", Confidence = approved ? "high" : "low"
            });
        }
        Aliases(reader, document.Callsigns.Prefixes);
        Aliases(reader, document.Callsigns.FullCallsigns);
        var hashes = Count(reader);
        for (var i = 0; i < hashes; i++)
        {
            document.AudioBankHashes.Add(Text(reader), Text(reader));
        }
        var sequences = Count(reader);
        for (var i = 0; i < sequences; i++)
        {
            var sequence = new SpeechSequence
            {
                Id = Text(reader), Name = Text(reader), AirportCode = Text(reader), IntentKey = Text(reader), Speaker = Text(reader)
            };
            var steps = Count(reader);
            for (var j = 0; j < steps; j++)
            {
                var kind = Text(reader);
                var text = Text(reader);
                var id = Reference(reader);
                sequence.Steps.Add(new() { Kind = kind, Text = text, Bank = id.Bank, Index = id.Index, AirportCode = id.AirportCode });
            }
            document.Sequences.Add(sequence);
        }
        if (memory.Position != memory.Length)
        {
            throw new InvalidDataException("Unexpected compiled speech fields.");
        }
        SpeechDataStore.Validate(document);
        return document;
    }

    private static Package Read(string data)
    {
        if (!File.Exists(FilePath(data)))
        {
            throw new FileNotFoundException("Compiled speech data is missing. Build SPEECH.BIN in Tower Tools Speech Workbench.", FilePath(data));
        }
        try
        {
            using var file = new FileStream(FilePath(data), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (file.Length > MaxBytes + 128)
            {
                throw new InvalidDataException("Compiled speech file is too large.");
            }
            using var reader = new BinaryReader(file, Utf8);
            if (!Exact(reader, 4).SequenceEqual("TWSP"u8.ToArray()) || reader.ReadInt32() != Version)
            {
                throw new InvalidDataException("Unsupported compiled speech format.");
            }
            var sourceHash = Exact(reader, 32);
            var checksum = Exact(reader, 32);
            var length = reader.ReadInt32();
            var packed = reader.ReadInt32();
            if (length is < 1 or > MaxBytes || packed is < 1 or > MaxBytes || file.Length - file.Position != packed)
            {
                throw new InvalidDataException("Invalid compiled speech size.");
            }
            using var compressed = new MemoryStream(Exact(reader, packed), false);
            using var brotli = new BrotliStream(compressed, CompressionMode.Decompress);
            var bytes = new byte[length];
            brotli.ReadExactly(bytes);
            if (brotli.ReadByte() != -1 || !CryptographicOperations.FixedTimeEquals(checksum, SHA256.HashData(bytes)))
            {
                throw new InvalidDataException("Compiled speech checksum mismatch.");
            }
            return new(Decode(bytes), sourceHash);
        }
        catch (Exception exception) when (exception is EndOfStreamException or ArgumentException)
        {
            throw new InvalidDataException("Invalid compiled speech data. Rebuild SPEECH.BIN in the Workbench.", exception);
        }
    }

    private static void Write(string data, Package package, string? backupDirectory = null)
    {
        var bytes = Encode(package.Document);
        using var compressed = new MemoryStream();
        using (var brotli = new BrotliStream(compressed, CompressionLevel.SmallestSize, true))
        {
            brotli.Write(bytes);
        }
        // Exercise the reader's bounds before replacing a previously usable build.
        if (compressed.Length > MaxBytes)
        {
            throw new InvalidDataException("Compiled speech file is too large.");
        }
        _ = Decode(bytes);
        var path = FilePath(data);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            using (var writer = new BinaryWriter(file, Utf8))
            {
                writer.Write("TWSP"u8);
                writer.Write(Version);
                writer.Write(package.SourceHash);
                writer.Write(SHA256.HashData(bytes));
                writer.Write(bytes.Length);
                writer.Write((int)compressed.Length);
                writer.Write(compressed.ToArray());
                writer.Flush();
                file.Flush(true);
            }
            if (File.Exists(path))
            {
                var backup = backupDirectory ?? Path.Combine(Directory.GetParent(Path.GetFullPath(data))!.FullName, "Backups", "SpeechBuild");
                Directory.CreateDirectory(backup);
                File.Replace(temporary, path, Path.Combine(backup, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + "-" + Guid.NewGuid().ToString("N") + ".bak"));
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
}
