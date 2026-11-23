namespace MonkeyStack.Core.Timing;

using System.Diagnostics;

/// <summary>
/// Az <see cref="IFrameLoop"/> megvalósítása: alapértelmezés szerint ~60 Hz-es, <see cref="TimeProvider"/>-alapú léptető
/// (Active Object minta: saját aszinkron ciklusa van, a hívók csak feliratkoznak). Az eltelt időt a
/// <see cref="TimeProvider"/> órájából méri, így a tényleges ütemezési csúszást is továbbadja; tesztben hamis órával
/// determinisztikusan léptethető. A műveletek szálbiztosak (Thread-Safe Interface).
/// </summary>
public sealed class FrameLoop : IFrameLoop, IDisposable
{
    /// <summary>Az alapértelmezett ütemköz: 60 ütem másodpercenként.</summary>
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(1.0 / 60);

    private readonly object _syncRoot = new();
    private readonly TimeProvider _timeProvider;
    private CancellationTokenSource? _running;
    private bool _disposed;

    /// <param name="timeProvider">Az óra; ha <c>null</c>, <see cref="TimeProvider.System"/>.</param>
    /// <param name="interval">Az ütemköz; ha <c>null</c>, <see cref="DefaultInterval"/>.</param>
    public FrameLoop(TimeProvider? timeProvider = null, TimeSpan? interval = null)
    {
        Interval = interval ?? DefaultInterval;
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(Interval, TimeSpan.Zero, nameof(interval));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public event EventHandler<FrameTickEventArgs>? Tick;

    /// <summary>Az ütemköz.</summary>
    public TimeSpan Interval { get; }

    /// <summary>Fut-e a léptető (<see cref="Start"/> után, <see cref="Stop"/> vagy <see cref="Dispose"/> előtt).</summary>
    public bool IsRunning
    {
        get
        {
            lock (_syncRoot)
            {
                return _running is not null;
            }
        }
    }

    /// <summary>Elindítja a léptetőt a háttérben; ha már fut, nem csinál semmit.</summary>
    public void Start()
    {
        lock (_syncRoot)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_running is not null)
            {
                return;
            }

            _running = new CancellationTokenSource();
            var token = _running.Token;
            _ = Task.Run(() => RunAsync(token), CancellationToken.None);
        }
    }

    /// <summary>
    /// Leállítja a léptetőt; később újraindítható. Egy éppen futó ütem még befejeződik, utána új ütem nem indul.
    /// </summary>
    public void Stop()
    {
        CancellationTokenSource? running;
        lock (_syncRoot)
        {
            running = _running;
            _running = null;
        }

        // A záron kívül: a Cancel a ciklus folytatását ezen a szálon is futtathatja.
        running?.Cancel();
        running?.Dispose();
    }

    /// <summary>Leállítja a léptetőt; utána nem indítható újra.</summary>
    public void Dispose()
    {
        lock (_syncRoot)
        {
            _disposed = true;
        }

        Stop();
    }

    private async Task RunAsync(CancellationToken token)
    {
        try
        {
            using var timer = new PeriodicTimer(Interval, _timeProvider);
            long last = _timeProvider.GetTimestamp();
            while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
            {
                // A várakozás és a leállítás versenyében a már leállított léptető nem küld több ütemet.
                if (token.IsCancellationRequested)
                {
                    break;
                }

                long now = _timeProvider.GetTimestamp();
                var delta = _timeProvider.GetElapsedTime(last, now);
                last = now;
                RaiseTick(delta);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <summary>A feliratkozók egymástól függetlenül futnak: egy feliratkozó kivétele nem éri el a többit.</summary>
    private void RaiseTick(TimeSpan delta)
    {
        if (Tick is not { } handlers)
        {
            return;
        }

        var args = new FrameTickEventArgs(delta);
        foreach (var handler in handlers.GetInvocationList().Cast<EventHandler<FrameTickEventArgs>>())
        {
            try
            {
                handler(this, args);
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Egy FrameLoop.Tick feliratkozó hibát dobott: {ex}");
            }
        }
    }
}
