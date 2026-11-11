using System.Speech.Synthesis;
using TowerSpeech.Core;

namespace TowerSpeech.WindowsAudio;

public static class WindowsSpeechRenderer
{
    public static IReadOnlyList<string> InstalledVoices()
    {
        using var synth = new SpeechSynthesizer();
        return synth.GetInstalledVoices()
            .Where(v => v.Enabled && v.VoiceInfo.Culture.TwoLetterISOLanguageName == "en")
            .Select(v => v.VoiceInfo.Name)
            .OrderBy(v => v, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    /// <summary>Generate one RIFF-WAV from a whole sentence; no VOC recordings involved.</summary>
    public static byte[] Render(string text, SpeechPreferences options) =>
        Task.Run(() => RenderAsync(text, options.Copy())).GetAwaiter().GetResult();

    public static async Task<byte[]> RenderAsync(string text, SpeechPreferences options, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("No speech to play");
        }

        cancellationToken.ThrowIfCancellationRequested();
        using var wave = new MemoryStream();
        using var synth = new SpeechSynthesizer();
        var voice = string.IsNullOrWhiteSpace(options.VoiceName)
            ? InstalledVoices().FirstOrDefault() ?? throw new InvalidOperationException("Install an English Windows desktop speech voice.")
            : options.VoiceName;
        synth.SelectVoice(voice);
        synth.Rate = Math.Clamp(options.Rate, -10, 10);
        synth.SetOutputToWaveStream(wave);
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<SpeakCompletedEventArgs> handler = (_, e) =>
        {
            if (e.Error is { } error)
            {
                completed.TrySetException(error);
            }
            else if (e.Cancelled)
            {
                completed.TrySetCanceled();
            }
            else
            {
                completed.TrySetResult();
            }
        };
        synth.SpeakCompleted += handler;
        try
        {
            using var stop = cancellationToken.Register(synth.SpeakAsyncCancelAll);
            cancellationToken.ThrowIfCancellationRequested();
            synth.SpeakAsync(text);
            await completed.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (cancellationToken.IsCancellationRequested)
            {
                synth.SpeakAsyncCancelAll();
            }
            synth.SpeakCompleted -= handler;
        }
        cancellationToken.ThrowIfCancellationRequested();
        synth.SetOutputToNull();
        var result = wave.ToArray();
        return options.RadioEffect ? RadioWaveEffects.Apply(result, options.NoisePercent) : result;
    }
}
