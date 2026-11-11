using Tower.Infrastructure;
using TowerSpeech.Core;
using TowerSpeech.WindowsAudio;

namespace Tower.Core;

/// <summary>Connects simulation messages to the shared Workbench speech pipeline.</summary>
internal sealed class VoicePlayer : IDisposable
{
    private readonly RadioPlaybackQueue playback = new(new WindowsRadioPlaybackBackend());
    private WorkbenchSpeechRuntime? workbench;
    private string status = "Voice files not loaded";

    public VoicePlayer()
    {
        playback.StatusChanged += value => Volatile.Write(ref status, value);
    }

    public string SpeechWorkspace => workbench?.Workspace ?? WorkspacePaths.RuntimeRoot;
    public string Status => Volatile.Read(ref status);

    public IReadOnlyCollection<string>? GetTrafficCallsigns()
    {
        if (workbench is null)
        {
            throw new InvalidDataException("Speech data unavailable. Check Data/SPEECH.BIN and the VOC banks before starting a simulation.");
        }
        return workbench.GetTrafficCallsigns();
    }

    public void ReloadSpeechWorkspace()
    {
        ClearPending();
        workbench = null;
        try
        {
            workbench = new WorkbenchSpeechRuntime();
            Volatile.Write(ref status, "Workbench speech ready: " + workbench.Preferences.Engine);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        {
            Volatile.Write(ref status, "Speech unavailable: check Data/SPEECH.BIN and the original VOC banks.");
        }
    }

    public void Readback(AircraftRadioMessage message)
    {
        if (!Tower.WinForms.Program.Settings.SoundTalk)
        {
            return;
        }
        if (workbench is not { } runtime)
        {
            Volatile.Write(ref status, "Speech unavailable: check Data/SPEECH.BIN in the shared game Data folder.");
            return;
        }
        playback.Enqueue(() =>
        {
            var output = runtime.Render(message);
            return new(output.Wave, output.Wave is null ? output.Text : message.Text,
                runtime.Preferences, output.Diagnostic);
        });
    }

    public void ClearPending() => playback.Clear();
    public void Dispose() => playback.Dispose();
}
