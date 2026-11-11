namespace TowerSpeech.Core;

public enum SpeechEngineKind
{
    BaoOriginal, WindowsTts
}

public sealed class SpeechPreferences
{
    public SpeechEngineKind Engine { get; set; } = SpeechEngineKind.BaoOriginal;
    public string VoiceName { get; set; } = "";
    public int Rate { get; set; } = 0;
    public bool RadioEffect { get; set; } = false;
    public int NoisePercent { get; set; } = 5;
    public int ArrivalDistanceMiles { get; set; } = 10;

    public static string FilePath(string dataRoot) => SpeechDataStore.FilePath(dataRoot);
    public static SpeechPreferences Load(string dataRoot) => SpeechDataStore.Load(dataRoot).Preferences.Copy();
    public void Save(string dataRoot)
    {
        var copy = Copy();
        SpeechDataStore.Update(dataRoot, document => document.Preferences = copy);
    }
    public SpeechPreferences Copy() => (SpeechPreferences)MemberwiseClone();
}
