using System.Threading.Channels;

namespace TowerSpeech.Core;

public sealed record RadioPlayback(byte[]? Wave, string Text, SpeechPreferences? Preferences = null, string? Diagnostic = null);

public interface IRadioPlaybackBackend
{
    Task<byte[]> RenderAsync(string text, SpeechPreferences preferences, CancellationToken cancellationToken);
    Task PlayAsync(byte[] wave, CancellationToken cancellationToken);
}

/// <summary>Serializes rendering and playback away from the caller's thread.</summary>
public sealed class RadioPlaybackQueue : IDisposable
{
    private sealed record Request(int Generation, Func<RadioPlayback> Render);
    private readonly object gate = new();
    private readonly Channel<Request> channel;
    private readonly IRadioPlaybackBackend backend;
    private readonly CancellationTokenSource shutdown = new();
    private CancellationTokenSource transmission = new();
    private readonly Task worker;
    private int generation;
    private bool disposed;
    private string status = "Radio ready";

    public string Status => Volatile.Read(ref status);
    public event Action<string>? StatusChanged;

    public RadioPlaybackQueue(IRadioPlaybackBackend backend, int capacity = 32)
    {
        ArgumentNullException.ThrowIfNull(backend);
        this.backend = backend;
        channel = Channel.CreateBounded<Request>(new BoundedChannelOptions(capacity)
        {
            SingleReader = false,
            FullMode = BoundedChannelFullMode.Wait
        });
        worker = Task.Run(ProcessAsync);
    }

    public bool Enqueue(Func<RadioPlayback> render)
    {
        ArgumentNullException.ThrowIfNull(render);
        lock (gate)
        {
            if (disposed)
            {
                return false;
            }
            if (channel.Writer.TryWrite(new(generation, render)))
            {
                return true;
            }
        }
        Report("Radio queue full; transmission was not queued.");
        return false;
    }

    public bool EnqueueWave(byte[] wave, string text = "Original VOC") => Enqueue(() => new(wave, text));

    public bool EnqueueTts(string text, SpeechPreferences preferences)
    {
        var snapshot = preferences.Copy();
        return Enqueue(() => new(null, text, snapshot));
    }

    public void Clear()
    {
        CancellationTokenSource previous;
        lock (gate)
        {
            if (disposed)
            {
                return;
            }
            generation++;
            previous = transmission;
            transmission = new();
            while (channel.Reader.TryRead(out _))
            {
            }
        }
        previous.Cancel();
        previous.Dispose();
    }

    private void Report(string value)
    {
        Volatile.Write(ref status, value);
        StatusChanged?.Invoke(value);
    }

    private async Task ProcessAsync()
    {
        try
        {
            await foreach (var request in channel.Reader.ReadAllAsync(shutdown.Token).ConfigureAwait(false))
            {
                CancellationTokenSource active;
                lock (gate)
                {
                    if (disposed || request.Generation != generation)
                    {
                        continue;
                    }
                    active = CancellationTokenSource.CreateLinkedTokenSource(shutdown.Token, transmission.Token);
                }
                using (active)
                {
                    try
                    {
                        active.Token.ThrowIfCancellationRequested();
                        var output = request.Render();
                        active.Token.ThrowIfCancellationRequested();
                        if (output.Diagnostic is { } diagnostic)
                        {
                            Report(diagnostic);
                            continue;
                        }
                        var wave = output.Wave;
                        if (wave is null)
                        {
                            if (output.Preferences is not { Engine: SpeechEngineKind.WindowsTts } preferences)
                            {
                                Report("No complete original recording available; transmission skipped.");
                                continue;
                            }
                            wave = await backend.RenderAsync(output.Text, preferences, active.Token).ConfigureAwait(false);
                        }
                        active.Token.ThrowIfCancellationRequested();
                        Report((output.Wave is null ? "Windows voice: " : "Original VOC: ") + output.Text);
                        await backend.PlayAsync(wave, active.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (active.IsCancellationRequested) { }
                    catch (Exception exception)
                    {
                        Report("Speech playback failed: " + exception.Message);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested) { }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            channel.Writer.TryComplete();
        }
        shutdown.Cancel();
        worker.GetAwaiter().GetResult();
        transmission.Dispose();
        shutdown.Dispose();
    }
}
