namespace OsEventEngine.Notifications;

using System.Diagnostics;

/// <summary>
/// Összevonja a sűrű jelzéseket: az első <see cref="Signal"/> után <c>delay</c> idővel egyszer futtatja a műveletet,
/// a közben érkező jelzések nem indítanak újabbat. Így egy ablak megnyitásakor jövő tucatnyi WinEvent egyetlen mintavétel.
/// </summary>
internal sealed class Throttle : IDisposable
{
    private readonly ITimer _timer;
    private readonly Action _action;
    private readonly TimeSpan _delay;
    private int _pending;
    private int _disposed;

    public Throttle(TimeSpan delay, TimeProvider timeProvider, Action action)
    {
        _delay = delay;
        _action = action;
        _timer = timeProvider.CreateTimer(_ => Fire(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public void Signal()
    {
        if (Volatile.Read(ref _disposed) == 0 && Interlocked.Exchange(ref _pending, 1) == 0)
        {
            _timer.Change(_delay, Timeout.InfiniteTimeSpan);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _timer.Dispose();
        }
    }

    private void Fire()
    {
        Volatile.Write(ref _pending, 0);
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        try
        {
            _action();
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"[OsEventEngine] Értesítés feldolgozási hiba: {ex.Message}");
        }
    }
}
