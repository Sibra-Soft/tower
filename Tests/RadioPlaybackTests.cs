using System.Collections.Concurrent;
using TowerSpeech.Core;
using Xunit;

namespace Tower.Tests;

public sealed class RadioPlaybackTests
{
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static Task Wait(Task task) => task.WaitAsync(TimeSpan.FromSeconds(10));

    private sealed class Backend : IRadioPlaybackBackend
    {
        public Func<string, SpeechPreferences, CancellationToken, Task<byte[]>> Render
        {
            get; set;
        } =
            (_, _, _) => Task.FromResult(new byte[] { 1 });
        public Func<byte[], CancellationToken, Task> Play { get; set; } = (_, _) => Task.CompletedTask;
        public Task<byte[]> RenderAsync(string text, SpeechPreferences preferences, CancellationToken token) => Render(text, preferences, token);
        public Task PlayAsync(byte[] wave, CancellationToken token) => Play(wave, token);
    }

    [Fact]
    public async Task TransmissionsPlayInOrderWithoutOverlap()
    {
        var started = Signal();
        var release = Signal();
        var finished = Signal();
        var order = new ConcurrentQueue<byte>();
        var backend = new Backend
        {
            Play = async (wave, token) =>
            {
                order.Enqueue(wave[0]);
                if (wave[0] == 1)
                {
                    started.SetResult();
                    await release.Task.WaitAsync(token);
                }
                else
                {
                    finished.SetResult();
                }
            }
        };
        using var queue = new RadioPlaybackQueue(backend);
        queue.EnqueueWave([1]);
        await Wait(started.Task);
        queue.EnqueueWave([2]);
        Assert.Equal(new byte[] { 1 }, order.ToArray());
        release.SetResult();
        await Wait(finished.Task);
        Assert.Equal(new byte[] { 1, 2 }, order.ToArray());
    }

    [Fact]
    public async Task ClearCancelsActivePlaybackDropsOldMessagesAndAcceptsNewOnes()
    {
        var started = Signal();
        var cancelled = Signal();
        var finished = Signal();
        var backend = new Backend
        {
            Play = async (wave, token) =>
            {
                if (wave[0] == 1)
                {
                    started.SetResult();
                    try
                    {
                        await Task.Delay(Timeout.Infinite, token);
                    }
                    finally { cancelled.SetResult(); }
                }
                else
                {
                    Assert.Equal(3, wave[0]);
                    finished.SetResult();
                }
            }
        };
        using var queue = new RadioPlaybackQueue(backend);
        queue.EnqueueWave([1]);
        await Wait(started.Task);
        queue.EnqueueWave([2]);
        queue.Clear();
        queue.EnqueueWave([3]);
        await Wait(cancelled.Task);
        await Wait(finished.Task);
    }

    [Fact]
    public async Task TtsUsesSnapshotAndCanBeCancelledDuringSynthesis()
    {
        var started = Signal();
        var cancelled = Signal();
        var finished = Signal();
        var preferences = new SpeechPreferences { Engine = SpeechEngineKind.WindowsTts, VoiceName = "English", Rate = 2 };
        var backend = new Backend
        {
            Render = async (text, options, token) =>
            {
                Assert.Equal("English", options.VoiceName);
                Assert.Equal(2, options.Rate);
                started.SetResult();
                try
                {
                    await Task.Delay(Timeout.Infinite, token);
                }
                finally { cancelled.SetResult(); }
                return [];
            },
            Play = (_, _) => { finished.SetResult(); return Task.CompletedTask; }
        };
        using var queue = new RadioPlaybackQueue(backend);
        queue.EnqueueTts("Tower", preferences);
        preferences.VoiceName = "Changed";
        preferences.Rate = 9;
        await Wait(started.Task);
        queue.Clear();
        queue.EnqueueWave([3]);
        await Wait(cancelled.Task);
        await Wait(finished.Task);
    }

    [Fact]
    public async Task MissingOriginalAndDiagnosticsNeverFallBackToTts()
    {
        var finished = Signal();
        int renders = 0;
        int plays = 0;
        var backend = new Backend
        {
            Render = (_, _, _) => { Interlocked.Increment(ref renders); return Task.FromResult(new byte[] { 1 }); },
            Play = (_, _) => { Interlocked.Increment(ref plays); finished.SetResult(); return Task.CompletedTask; }
        };
        using var queue = new RadioPlaybackQueue(backend);
        queue.Enqueue(() => new(null, "Missing original", new SpeechPreferences()));
        queue.Enqueue(() => new(null, "Invalid", new() { Engine = SpeechEngineKind.WindowsTts }, "Incomplete template"));
        queue.EnqueueWave([1]);
        await Wait(finished.Task);
        Assert.Equal(0, renders);
        Assert.Equal(1, plays);
    }

    [Fact]
    public async Task PlaybackFailureDoesNotStopFollowingMessages()
    {
        var finished = Signal();
        var failure = Signal();
        var backend = new Backend
        {
            Play = (wave, _) =>
            {
                if (wave[0] == 1)
                    throw new IOException("Audio unavailable");
                finished.SetResult();
                return Task.CompletedTask;
            }
        };
        using var queue = new RadioPlaybackQueue(backend);
        queue.StatusChanged += status => { if (status.Contains("Audio unavailable")) failure.SetResult(); };
        queue.EnqueueWave([1]);
        queue.EnqueueWave([2]);
        await Wait(failure.Task);
        await Wait(finished.Task);
    }

    [Fact]
    public async Task FullQueueReportsRejectionAndDisposeCancelsActivePlayback()
    {
        var started = Signal();
        var cancelled = Signal();
        var backend = new Backend
        {
            Play = async (_, token) =>
            {
                started.SetResult();
                try
                {
                    await Task.Delay(Timeout.Infinite, token);
                }
                finally { cancelled.SetResult(); }
            }
        };
        using var queue = new RadioPlaybackQueue(backend, capacity: 1);
        Assert.True(queue.EnqueueWave([1]));
        await Wait(started.Task);
        Assert.True(queue.EnqueueWave([2]));
        Assert.False(queue.EnqueueWave([3]));
        Assert.Contains("queue full", queue.Status);
        queue.Dispose();
        await Wait(cancelled.Task);
        Assert.False(queue.EnqueueWave([4]));
        queue.Dispose();
    }
}
