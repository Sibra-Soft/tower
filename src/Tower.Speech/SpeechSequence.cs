using System.Text.Json;

namespace TowerSpeech.Core;

/// <summary>A human-edited radio phrase. Each part is an original whole VOC fragment.</summary>
public sealed class SpeechSequence
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public int SchemaVersion { get; set; } = 1;
    public string Name { get; set; } = "New transmission";
    public string AirportCode { get; set; } = "ORD";
    public string IntentKey { get; set; } = "";
    public string Speaker { get; set; } = "Pilot";
    public List<SpeechSequenceStep> Steps { get; set; } = [];
}
public sealed class SpeechSequenceStep
{
    // fragment, callsign or runway. Placeholders are populated at runtime by the aircraft state.
    public string Kind { get; set; } = "fragment";
    public string Text { get; set; } = ""; // Literal phrase for TTS; BAO must resolve to original audio or warn.
    public int Bank
    {
        get; set;
    }
    public int Index
    {
        get; set;
    }
    public string AirportCode { get; set; } = "";
    public FragmentId Id => new(Bank, Index, AirportCode);
    public static SpeechSequenceStep FromId(FragmentId id) => new()
    {
        Bank = id.Bank,
        Index = id.Index,
        AirportCode = id.AirportCode
    };
}
public static class SpeechSequenceRenderer
{
    public static SpeechSequence Load(string file)
    {
        var seq = JsonSerializer.Deserialize<SpeechSequence>(File.ReadAllText(file),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (seq?.SchemaVersion != 1)
        {
            throw new InvalidDataException("Invalid sequence schema version.");
        }

        return seq;
    }
}
