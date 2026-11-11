using System.Text.Json.Serialization;

namespace TowerSpeech.Core;

// The original schema is retained, without silently approving ASR candidates.
public class FragmentDocument
{
    [JsonRequired]
    public int SchemaVersion
    {
        get; set;
    }
    public List<VoiceFragment> Fragments { get; set; } = [];
    public List<ProposedMapping> ProposedMappings { get; set; } = [];
    public List<string> UnprocessedFiles { get; set; } = [];
}

public sealed class VoiceFragment
{
    public string FileName { get; set; } = "";
    public int Bank
    {
        get; set;
    }
    public int Index
    {
        get; set;
    }
    public string AirportCode { get; set; } = "";
    public string Expression { get; set; } = "";
    public string? Transcript
    {
        get; set;
    }
    public string Status { get; set; } = "uncertain";
    public string Confidence { get; set; } = "low";
    public List<string> Alternatives { get; set; } = [];
    public string Notes { get; set; } = "";
}
public sealed class ProposedMapping
{
    [JsonPropertyName("Text")]
    public string Text { get; set; } = "";
    [JsonPropertyName("Expression")]
    public string Expression { get; set; } = "";
    [JsonPropertyName("AirportCode")]
    public string AirportCode { get; set; } = "";
}
public readonly record struct FragmentId(int Bank, int Index, string AirportCode = "")
{
    public override string ToString() => $"({Bank}:{Index})" +
        (AirportCode.Length > 0 ? $"[{AirportCode}]" : "");
}
public enum VoiceMode
{
    Strict, ExperimentalCandidates
}
public enum RadioSpeaker
{
    Pilot, Controller, System
}
public enum NumberContext
{
    FlightNumber, Heading, Altitude, Distance, Runway, Speed, Cardinal
}

public abstract record SpeechPart;
// CatalogText preserves reviewed spellings such as "taxi two" while TTS says "taxi to".
public sealed record RecordedPhrase(string Text, FragmentId? Preferred = null, string? CatalogText = null) : SpeechPart;
public sealed record AircraftCallsign(string Value) : SpeechPart;
public sealed record RunwayNumber(string Value) : SpeechPart;
public sealed record SpokenDigits(string Digits, NumberContext Context) : SpeechPart;

public sealed record RadioMessage(
    string AircraftId,
    string AirportCode,
    RadioSpeaker Speaker,
    string DisplayText,
    IReadOnlyList<SpeechPart> Parts,
    string? Runway = null,
    string? IntentKey = null);

public sealed record VoicePiece(FragmentId Id, string FileName, string Text, bool IsCandidate);
public sealed record RenderedMessage(
    RadioMessage Source,
    IReadOnlyList<VoicePiece> Pieces,
    IReadOnlyList<string> Warnings,
    byte[]? WaveBytes);

