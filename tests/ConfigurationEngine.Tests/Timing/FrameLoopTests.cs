namespace ConfigurationEngine.Tests.Timing;

using System.Collections.Concurrent;
using Microsoft.Extensions.Time.Testing;
using MonkeyStack.Core.Timing;

/// <summary>
/// A Core-beli <see cref="FrameLoop"/> hamis órával (a Core-nak nincs saját tesztprojektje, a léptetőt elsőként a
/// Konfigurációs motor használja).
/// </summary>
public sealed class FrameLoopTests : IDisposable
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(10);

    private readonly FakeTimeProvider _time = new();
    private readonly FrameLoop _loop;
    private readonly ConcurrentQueue<TimeSpan> _deltas = new();

    public FrameLoopTests()
    {
        _loop = new FrameLoop(_time, Interval);
        _loop.Tick += (_, e) => _deltas.Enqueue(e.DeltaTime);
    }

    public void Dispose() => _loop.Dispose();

    [Fact]
    public void Defaults_To60Hz()
    {
        using var loop = new FrameLoop();

        Assert.Equal(TimeSpan.FromSeconds(1.0 / 60), loop.Interval);
        Assert.False(loop.IsRunning);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveInterval_IsRejected(int milliseconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FrameLoop(_time, TimeSpan.FromMilliseconds(milliseconds)));
    }

    [Fact]
    public async Task Tick_ReportsElapsedTime()
    {
        _loop.Start();
        await AdvanceUntilTicks(1);
        int before = _deltas.Count;

        // Az ütemek a ténylegesen eltelt időt adják tovább: összegük pontosan a léptetett idő, akárhány ütemben érkezik.
        _time.Advance(3 * Interval);
        await WaitUntil(() => Elapsed(before) >= 3 * Interval);
        await Task.Delay(50);

        Assert.Equal(3 * Interval, Elapsed(before));
    }

    [Fact]
    public async Task ThrowingSubscriber_DoesNotStopTheLoopOrOthers()
    {
        _loop.Tick += (_, _) => throw new InvalidOperationException("hibás feliratkozó");
        _loop.Start();

        await AdvanceUntilTicks(3);

        Assert.True(_loop.IsRunning);
    }

    [Fact]
    public async Task Stop_EndsTicks_AndStartResumes()
    {
        _loop.Start();
        await AdvanceUntilTicks(1);

        _loop.Stop();
        int stoppedAt = _deltas.Count;
        _time.Advance(10 * Interval);
        await Task.Delay(100);

        Assert.False(_loop.IsRunning);
        Assert.Equal(stoppedAt, _deltas.Count);

        _loop.Start();
        await AdvanceUntilTicks(stoppedAt + 1);
    }

    [Fact]
    public void Start_AfterDispose_Throws()
    {
        _loop.Dispose();

        Assert.Throws<ObjectDisposedException>(_loop.Start);
    }

    /// <summary>
    /// Ütemközönként lépteti az órát, amíg legalább <paramref name="count"/> ütem meg nem érkezik. A léptető saját
    /// szálon indul, ezért az első ütem csak akkor jön, amikor az időzítője már létrejött.
    /// </summary>
    private async Task AdvanceUntilTicks(int count)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (_deltas.Count < count)
        {
            Assert.True(DateTime.UtcNow < deadline, "A léptető nem küldött ütemet időben.");
            int before = _deltas.Count;
            _time.Advance(Interval);
            for (int i = 0; i < 20 && _deltas.Count == before; i++)
            {
                await Task.Delay(5);
            }
        }
    }

    private TimeSpan Elapsed(int skip) => _deltas.Skip(skip).Aggregate(TimeSpan.Zero, (sum, delta) => sum + delta);

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "A léptető nem küldött ütemet időben.");
            await Task.Delay(5);
        }
    }
}
