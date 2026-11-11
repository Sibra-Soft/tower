using System.Text.Json;
using System.Text.RegularExpressions;

namespace TowerSpeech.Core;

public sealed class VoiceCatalog
{
    private readonly Dictionary<FragmentId, VoiceFragment> _byId;
    private readonly Dictionary<string, List<VoiceFragment>> _byText;
    public int Count => _byId.Count;
    public static VoiceCatalog FromFragments(IEnumerable<VoiceFragment> fragments) => new(fragments);

    private VoiceCatalog(IEnumerable<VoiceFragment> fragments)
    {
        _byId = new();
        _byText = new(StringComparer.OrdinalIgnoreCase);
        foreach (var fragment in fragments)
        {
            string airport = fragment.Bank <= 2 ? "" : fragment.AirportCode.ToUpperInvariant();
            var id = new FragmentId(fragment.Bank, fragment.Index, airport);
            if (!_byId.TryAdd(id, fragment))
            {
                throw new InvalidDataException($"Duplicate fragment reference: {id}");
            }

            if (string.IsNullOrWhiteSpace(fragment.Transcript))
            {
                continue;
            }

            var key = Normalize(fragment.Transcript);
            if (!_byText.TryGetValue(key, out var list))
            {
                _byText[key] = list = [];
            }

            list.Add(fragment);
        }
    }

    public static VoiceCatalog Load(string combinedJsonPath)
    {
        var document = JsonSerializer.Deserialize<FragmentDocument>(
            File.ReadAllText(combinedJsonPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("Empty catalog");
        if (document.SchemaVersion != 1)
        {
            throw new InvalidDataException("Unknown JSON schema version");
        }

        return new VoiceCatalog(document.Fragments);
    }

    public static string Normalize(string text) => Regex.Replace(
        text.Trim().ToLowerInvariant(), @"\s+", " ");

    public VoiceFragment? Get(FragmentId id) => _byId.GetValueOrDefault(id);

    private static bool Allowed(VoiceFragment fragment, VoiceMode mode) =>
        fragment.Status == "clear" && fragment.Confidence == "high" ||
        mode == VoiceMode.ExperimentalCandidates &&
        !string.IsNullOrWhiteSpace(fragment.Transcript) &&
        fragment.Status is "uncertain" or "partial";

    public bool TryResolve(
        string text, string airportCode, VoiceMode mode, FragmentId? preferred,
        out VoicePiece? piece, out string? issue)
    {
        piece = null;
        issue = null;
        var key = Normalize(text);
        var airport = airportCode.ToUpperInvariant();
        if (preferred is FragmentId id)
        {
            var f = Get(id);
            if (f is null || Normalize(f.Transcript ?? "") != key)
            {
                issue = $"{text}: explicit reference {id} is missing or its transcript differs";
                return false;
            }
            if (f.Bank is >= 3 and <= 5 && !string.Equals(f.AirportCode, airport, StringComparison.OrdinalIgnoreCase))
            {
                issue = $"{text}: {id} belongs to airport {f.AirportCode}, not {airport}";
                return false;
            }
            if (!Allowed(f, mode))
            {
                issue = $"{text}: {id} not approved (status {f.Status}, {f.Confidence})";
                return false;
            }
            piece = MakePiece(f);
            return true;
        }
        if (!_byText.TryGetValue(key, out var matches))
        {
            issue = $"'{text}': no text mapping in the VOC catalog";
            return false;
        }
        // Original global banks and generated extensions work at every airport.
        var candidates = matches.Where(f => f.Bank <= 2 || f.Bank == 6 ||
            string.Equals(f.AirportCode, airport, StringComparison.OrdinalIgnoreCase))
            .Where(f => Allowed(f, mode)).ToArray();
        if (candidates.Length == 0)
        {
            issue = $"'{text}': no permitted mapping in mode {mode}";
            return false;
        }
        if (candidates.Length > 1)
        {
            issue = $"'{text}': multiple possible fragments ({string.Join(", ", candidates.Select(f => f.Expression))}); choose explicitly";
            return false;
        }
        piece = MakePiece(candidates[0]);
        return true;
    }

    private static VoicePiece MakePiece(VoiceFragment f) => new(
        new FragmentId(f.Bank, f.Index, f.Bank <= 2 ? "" : f.AirportCode),
        f.FileName, f.Transcript!, f.Status != "clear" || f.Confidence != "high");
}
