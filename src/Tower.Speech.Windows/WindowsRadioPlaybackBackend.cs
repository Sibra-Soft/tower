using System.Media;
using TowerSpeech.Core;

namespace TowerSpeech.WindowsAudio;

public sealed class WindowsRadioPlaybackBackend : IRadioPlaybackBackend
{
    public Task<byte[]> RenderAsync(string text, SpeechPreferences preferences, CancellationToken cancellationToken) =>
        WindowsSpeechRenderer.RenderAsync(text, preferences, cancellationToken);

    public Task PlayAsync(byte[] wave, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream(wave, writable: false);
        using var player = new SoundPlayer(stream);
        player.Load();
        using var stop = cancellationToken.Register(player.Stop);
        cancellationToken.ThrowIfCancellationRequested();
        player.PlaySync();
        return Task.CompletedTask;
    }
}
