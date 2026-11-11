using System.Text.Json;

namespace TowerSpeech.Core;

// User-maintained alias table. An alias is not a transcript; it only binds an identifier
// to an existing (separately approved) callsign recording.
public sealed class CallsignLexicon
{
    public sealed class Alias
    {
        public string Text { get; set; } = "";
        public int Bank
        {
            get; set;
        }
        public int Index
        {
            get; set;
        }
        public string AirportCode { get; set; } = "";
    }
    public Dictionary<string, Alias> Prefixes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    // Entire recorded callsigns have priority over prefix + synthetic digits.
    // User-editable ASR candidates, never automatically marked as verified.
    public Dictionary<string, Alias> FullCallsigns { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public static CallsignLexicon Load(string path)
    {
        if (!File.Exists(path))
        {
            return new();
        }

        var result = JsonSerializer.Deserialize<CallsignLexicon>(File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
        result.Prefixes = new Dictionary<string, Alias>(result.Prefixes ?? new(), StringComparer.OrdinalIgnoreCase);
        result.FullCallsigns = new Dictionary<string, Alias>(result.FullCallsigns ?? new(), StringComparer.OrdinalIgnoreCase);
        return result;
    }

    public bool TryGetFull(string callsign, out Alias? alias)
    {
        alias = FullCallsigns.GetValueOrDefault(callsign.Trim().ToUpperInvariant());
        return alias is { Bank: 1, Index: > 0, AirportCode: "" } && !string.IsNullOrWhiteSpace(alias.Text);
    }

    public bool TryGet(string prefix, out Alias? alias)
    {
        alias = Prefixes.GetValueOrDefault(prefix.ToUpperInvariant());
        return alias is { Bank: > 0, Index: > 0 } && !string.IsNullOrWhiteSpace(alias.Text);
    }
}
