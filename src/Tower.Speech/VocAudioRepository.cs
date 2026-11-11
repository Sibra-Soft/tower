using System.Buffers.Binary;

namespace TowerSpeech.Core;

/// <summary>
/// BAO's indexed raw VOC banks (not Creative Voice File):
/// [01][24-bit block size][91 00][unsigned PCM...], followed by a table of
/// (count = fragments + 1) absolute byte offsets and a [count:u16][version:u16] footer.
/// The user's original bank numbering starts at 1. Never interpolate or infer indices.
/// </summary>
public sealed class VocArchive
{
    private readonly byte[] _file;
    private readonly (int Offset, int Length)[] _samples;
    public int FragmentCount => _samples.Length;
    public int SampleRate => WaveComposer.SampleRate;

    public VocArchive(string path)
    {
        _file = File.ReadAllBytes(path);
        if (_file.Length < 12)
        {
            throw new InvalidDataException("VOC is too short: " + path);
        }

        ushort count = BinaryPrimitives.ReadUInt16LittleEndian(_file.AsSpan(_file.Length - 4));
        ushort version = BinaryPrimitives.ReadUInt16LittleEndian(_file.AsSpan(_file.Length - 2));
        if (version != 1 || count < 2 || count > 5000)
        {
            throw new InvalidDataException("VOC has no supported BAO index table: " + path);
        }

        int table = checked(_file.Length - 4 - count * 4);
        if (table < 0)
        {
            throw new InvalidDataException("Invalid VOC index table");
        }

        var offsets = new int[count];
        for (int i = 0; i < count; i++)
        {
            uint value = BinaryPrimitives.ReadUInt32LittleEndian(_file.AsSpan(table + i * 4, 4));
            if (value > int.MaxValue || value > table)
            {
                throw new InvalidDataException("VOC offset out of range");
            }

            offsets[i] = (int)value;
        }
        if (offsets[0] != 0 || offsets[^1] != table)
        {
            throw new InvalidDataException("Invalid VOC start or end offset");
        }

        _samples = new (int, int)[count - 1];
        for (int i = 0; i < _samples.Length; i++)
        {
            int start = offsets[i], end = offsets[i + 1];
            if (end <= start || end - start < 6 || end > table)
            {
                throw new InvalidDataException($"VOC-fragment {i + 1}: invalid boundaries");
            }

            int blockSize = _file[start + 1] | _file[start + 2] << 8 | _file[start + 3] << 16;
            if (_file[start] != 0x01 || _file[start + 4] != 0x91 || _file[start + 5] != 0x00 ||
                blockSize != end - start - 4)
            {
                throw new InvalidDataException($"VOC-fragment {i + 1}: unexpected block header");
            }

            _samples[i] = (start + 6, end - start - 6);
        }
    }

    public byte[] ReadPcm(int oneBasedIndex)
    {
        if (oneBasedIndex < 1 || oneBasedIndex > _samples.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(oneBasedIndex), $"VOC contains {_samples.Length} fragments");
        }

        var (offset, length) = _samples[oneBasedIndex - 1];
        return _file.AsSpan(offset, length).ToArray();
    }
    public double DurationSeconds(int oneBasedIndex)
    {
        if (oneBasedIndex < 1 || oneBasedIndex > _samples.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(oneBasedIndex));
        }

        return _samples[oneBasedIndex - 1].Length / (double)SampleRate;
    }
}

/// <summary>Direct, indexed VOC access. No extracted WAV files are needed or used.</summary>
public sealed class VocAudioRepository
{
    private readonly string _folder;
    private readonly Dictionary<string, VocArchive> _archives = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sync = new();
    public VocAudioRepository(string dataRoot)
    {
        _folder = WorkspacePaths.DataFolder(dataRoot);
    }
    public string SourceFile(FragmentId id)
    {
        string file = id.Bank switch
        {
            1 when id.AirportCode == "" => "CALLSIGN.VOC",
            2 when id.AirportCode == "" => "VOICE.VOC",
            6 when id.AirportCode == "" => "GENERATED.VOC",
            >= 3 and <= 5 when System.Text.RegularExpressions.Regex.IsMatch(id.AirportCode, @"^[A-Za-z0-9]{3,8}$")
                => id.AirportCode.ToUpperInvariant() + (id.Bank == 3 ? "F.VOC" : id.Bank == 4 ? "R.VOC" : "T.VOC"),
            _ => throw new InvalidDataException($"Unknown VOC bank/airport: {id}")
        };
        return id.Bank <= 2 || id.Bank == 6 ? Path.Combine(_folder, file) : Path.Combine(_folder, id.AirportCode.ToUpperInvariant(), file);
    }

    public FragmentId? FindRunway(string airportCode, string runway)
    {
        var airport = airportCode.ToUpperInvariant();
        // SourceFile validates the airport before it is used in a filesystem path.
        var folder = Path.GetDirectoryName(SourceFile(new(4, 1, airport)))!;
        var path = Path.Combine(folder, airport + ".SPF");
        if (!File.Exists(path))
        {
            return null;
        }
        var inRunways = false;
        foreach (var line in File.ReadLines(path))
        {
            var text = line.Split(';')[0].Trim();
            if (text.StartsWith('['))
            {
                inRunways = text.Equals("[RUNWAYS]", StringComparison.OrdinalIgnoreCase);
                continue;
            }
            if (!inRunways)
            {
                continue;
            }
            var fields = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length == 2 && fields[0].Equals("RUNWAY_" + runway, StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(fields[1], out var index) && index > 0)
            {
                return new(4, index, airport);
            }
        }
        return null;
    }

    public bool UsesRunwayDigits(string airportCode, string runway)
        => string.Equals(RunwayDigitOption(airportCode, "RUNWAY_" + runway), "On", StringComparison.OrdinalIgnoreCase);

    public string? RunwayDigitPrefix(string airportCode) => RunwayDigitOption(airportCode, "Prefix");

    private string? RunwayDigitOption(string airportCode, string key)
    {
        var airport = airportCode.ToUpperInvariant();
        var folder = Path.GetDirectoryName(SourceFile(new(4, 1, airport)))!;
        var path = Path.Combine(folder, airport + ".SPF");
        if (!File.Exists(path))
        {
            return null;
        }
        var inSection = false;
        foreach (var raw in File.ReadLines(path))
        {
            var text = raw.Split(';')[0].Trim();
            if (text.StartsWith('['))
            {
                inSection = text.Equals("[RunwayDigits]", StringComparison.OrdinalIgnoreCase);
                continue;
            }
            var fields = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (inSection && fields.Length == 2 && fields[0].Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return fields[1];
            }
        }
        return null;
    }
    public void ValidateSources(SpeechDocument document) => ValidateSources(document, null);

    public void ValidateSources(SpeechDocument document, Func<string, bool>? airportInstalled)
    {
        foreach (var fragment in document.Fragments.DistinctBy(f => (f.Bank, f.AirportCode)))
        {
            if (fragment.Bank is >= 3 and <= 5 && airportInstalled is not null && !airportInstalled(fragment.AirportCode))
            {
                continue;
            }
            var path = SourceFile(new FragmentId(fragment.Bank, fragment.Index, fragment.AirportCode));
            var relative = Path.GetRelativePath(_folder, path).Replace('\\', '/');
            if (document.AudioBankHashes.TryGetValue(relative, out var expected))
            {
                using var stream = File.OpenRead(path);
                var actual = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream));
                if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("The VOC bank differs from the reviewed recording: " + relative + ". Review data has been preserved.");
                }
            }
        }
    }
    private VocArchive Open(FragmentId id)
    {
        string path = SourceFile(id);
        lock (_sync)
        {
            if (!_archives.TryGetValue(path, out var archive))
            {
                _archives[path] = archive = new VocArchive(path);
            }

            return archive;
        }
    }
    public int GetFragmentCount(FragmentId id) => Open(id).FragmentCount;
    public double DurationSeconds(FragmentId id) => Open(id).DurationSeconds(id.Index);
    public byte[] ReadPcm(FragmentId id) => Open(id).ReadPcm(id.Index);
    public byte[] ReadWav(FragmentId id) => WaveComposer.WrapPcm(ReadPcm(id));
    public bool HasFragment(FragmentId id)
    {
        try
        {
            return id.Index >= 1 && id.Index <= Open(id).FragmentCount;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException) { return false; }
    }
}
