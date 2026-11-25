namespace OsEventEngine.Platforms;

using System.Diagnostics;
using System.Threading.Channels;
using MonkeyStack.Core.Character;
using OsEventEngine.Native;
using OsEventEngine.Notifications;

/// <summary>
/// Az <see cref="IPlatformProvider"/> megvalósítása (M11, docs/os_event_engine/platform_provider_proposal.md):
/// a látható ablakperemekből és a monitoronkénti talajból számolt <see cref="PlatformSnapshot"/>.
/// <list type="bullet">
/// <item><b>Half-Sync/Half-Async:</b> a Reactor (async réteg) csak jelez (<see cref="MarkDirty"/>), az újraszámolás egy háttérciklusban
/// fut, a fizika (sync réteg) pedig a kész pillanatképet olvassa lock nélkül (<see cref="Current"/>).</item>
/// <item><b>Összevonás:</b> ablakhúzás közben a mozgásértesítések sűrűk, ezért legfeljebb <see cref="MinRefreshInterval"/>-onként számolunk.</item>
/// <item><b>Biztonsági háló:</b> ha nem jön értesítés, <see cref="SafetyInterval"/>-onként akkor is újraszámolunk.</item>
/// <item>Csak változáskor nő a <see cref="PlatformSnapshot.Version"/> és jön <see cref="PlatformsChanged"/>.</item>
/// </list>
/// </summary>
internal sealed class Win32PlatformProvider : IPlatformProvider, IDisposable
{
    /// <summary>Két újraszámolás között legalább ennyi idő telik el (~30 Hz).</summary>
    public static readonly TimeSpan MinRefreshInterval = TimeSpan.FromMilliseconds(33);

    /// <summary>Értesítés nélkül is ennyi időnként újraszámolunk.</summary>
    public static readonly TimeSpan SafetyInterval = TimeSpan.FromMilliseconds(500);

    private static readonly OsNotification[] Triggers =
    [
        OsNotification.WindowsChanged,
        OsNotification.WindowMoved,
        OsNotification.DisplayChanged,
        OsNotification.Resumed,
    ];

    private readonly IDesktopScanner _scanner;
    private readonly IOsNotificationSource? _notifications;
    private readonly TimeProvider _timeProvider;
    private readonly Channel<bool> _dirty = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

    private readonly object _lifecycle = new();
    private readonly List<IDisposable> _registrations = [];
    private PlatformSnapshot _current = PlatformSnapshot.Empty;
    private CancellationTokenSource? _cts;
    private Task _loop = Task.CompletedTask;
    private bool _disposed;

    /// <param name="scanner">Az asztal lekérdezése (Wrapper Facade; tesztekben hamis).</param>
    /// <param name="notifications">OS értesítések (Reactor); ha <c>null</c>, csak a biztonsági háló szerint frissül.</param>
    /// <param name="timeProvider">Időzítés (tesztekben <c>FakeTimeProvider</c>).</param>
    public Win32PlatformProvider(IDesktopScanner scanner, IOsNotificationSource? notifications, TimeProvider? timeProvider = null)
    {
        _scanner = scanner ?? throw new ArgumentNullException(nameof(scanner));
        _notifications = notifications;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public PlatformSnapshot Current => Volatile.Read(ref _current);

    /// <inheritdoc/>
    /// <remarks>A háttérszálon (vagy a <see cref="Start"/> hívójának szálán az első számításnál) váltódik ki.</remarks>
    public event EventHandler<PlatformSnapshot>? PlatformsChanged;

    /// <summary>
    /// Elindítja a figyelést. Az első pillanatképet szinkron számolja ki, így a <see cref="Current"/> azonnal használható.
    /// </summary>
    public void Start()
    {
        lock (_lifecycle)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_cts is not null)
            {
                return;
            }

            _cts = new CancellationTokenSource();
            if (_notifications is not null)
            {
                foreach (var trigger in Triggers)
                {
                    _registrations.Add(_notifications.Register(trigger, MarkDirty));
                }
            }
        }

        Refresh();
        var token = _cts.Token;
        _loop = Task.Run(() => RunAsync(token), token);
    }

    /// <summary>Újraszámolás kérése (a Reactor hívja). Nem blokkol; a sűrű jelzések összevonódnak.</summary>
    public void MarkDirty() => _dirty.Writer.TryWrite(true);

    /// <summary>
    /// Egy újraszámolás: lekérdezés, számítás, és ha változott, új pillanatkép és esemény.
    /// Hibánál a legutóbbi pillanatkép marad érvényben.
    /// </summary>
    internal void Refresh()
    {
        PlatformSnapshot next;
        try
        {
            var (platforms, screens) = PlatformCalculator.Calculate(_scanner.Scan());
            var previous = Current;
            if (previous.Version > 0 && PlatformCalculator.SameAs(previous, platforms, screens))
            {
                return;
            }

            next = new PlatformSnapshot(platforms, screens, previous.Version + 1);
            Volatile.Write(ref _current, next);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"[OsEventEngine] Platform számítási hiba: {ex.Message}");
            return;
        }

        var handlers = PlatformsChanged;
        if (handlers is null)
        {
            return;
        }

        foreach (EventHandler<PlatformSnapshot> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(this, next);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"[OsEventEngine] PlatformsChanged feliratkozó hiba: {ex.Message}");
            }
        }
    }

    public void Dispose()
    {
        Task loop;
        lock (_lifecycle)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            foreach (var registration in _registrations)
            {
                registration.Dispose();
            }

            _registrations.Clear();
            _cts?.Cancel();
            _dirty.Writer.TryComplete();
            loop = _loop;
        }

        try
        {
            loop.Wait(TimeSpan.FromSeconds(1));
        }
        catch (AggregateException)
        {
            // A leállítás nem dobhat kivételt.
        }

        _cts?.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await WaitForDirtyAsync(cancellationToken).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            Refresh();

            try
            {
                await Task.Delay(MinRefreshInterval, _timeProvider, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>Vár egy jelzésig vagy a biztonsági háló idejéig, amelyik előbb jön.</summary>
    private async Task WaitForDirtyAsync(CancellationToken cancellationToken)
    {
        using var safety = new CancellationTokenSource(SafetyInterval, _timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, safety.Token);
        try
        {
            if (await _dirty.Reader.WaitToReadAsync(linked.Token).ConfigureAwait(false))
            {
                _dirty.Reader.TryRead(out _);
            }
        }
        catch (OperationCanceledException)
        {
            // Lejárt a biztonsági háló, vagy leállítás.
        }
    }
}
