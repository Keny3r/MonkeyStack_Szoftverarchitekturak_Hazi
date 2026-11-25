namespace OsEventEngine.Native;

using System.Diagnostics;
using System.Windows.Interop;
using System.Windows.Threading;
using OsEventEngine.Notifications;

/// <summary>
/// Reactor (POSA2): egy dedikált STA szálon üzenethurkot futtat, és több Windows eseményforrást demultiplexál
/// a regisztrált handlerekre (<see cref="OsNotification"/> szerint):
/// <list type="bullet">
/// <item><c>SetWinEventHook</c>: ablak megjelenés / eltűnés / megszűnés, minimalizálás, előtérbe kerülés, mozgás;</item>
/// <item>rejtett felső szintű ablak (<see cref="HwndSource"/>): <c>WM_POWERBROADCAST</c>, <c>WM_TIMECHANGE</c>,
/// <c>WM_DISPLAYCHANGE</c>, <c>WM_SETTINGCHANGE</c>, <c>WM_DPICHANGED</c>. Message-only ablak nem kapná meg a broadcast üzeneteket.</item>
/// </list>
/// A szál lustán, az első regisztrációkor indul (<see cref="Lazy{T}"/>, a DCLO helyes .NET-es formája).
/// Ha az indítás nem sikerül, a Reactor csendben inaktív marad: a pollozás biztonsági hálóként tovább működik.
/// </summary>
internal sealed class Win32EventReactor : IOsNotificationSource, IDisposable
{
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(1);

    private static readonly (uint Min, uint Max)[] HookRanges =
    [
        (NativeMethods.EVENT_SYSTEM_FOREGROUND, NativeMethods.EVENT_SYSTEM_FOREGROUND),
        (NativeMethods.EVENT_SYSTEM_MOVESIZEEND, NativeMethods.EVENT_SYSTEM_MOVESIZEEND),
        (NativeMethods.EVENT_SYSTEM_MINIMIZESTART, NativeMethods.EVENT_SYSTEM_MINIMIZEEND),
        (NativeMethods.EVENT_OBJECT_DESTROY, NativeMethods.EVENT_OBJECT_HIDE),
        (NativeMethods.EVENT_OBJECT_LOCATIONCHANGE, NativeMethods.EVENT_OBJECT_LOCATIONCHANGE),
    ];

    private readonly object _sync = new();
    private readonly Dictionary<OsNotification, Action[]> _handlers = [];
    private readonly Lazy<bool> _started;
    private readonly List<IntPtr> _hooks = [];

    // A delegáltat mezőben tartjuk, különben a GC összegyűjthetné, miközben a natív hook még hívja.
    private readonly NativeMethods.WinEventProc _winEventProc;

    private Thread? _thread;
    private Dispatcher? _dispatcher;
    private HwndSource? _window;
    private int _disposed;

    public Win32EventReactor()
    {
        _winEventProc = OnWinEvent;
        _started = new Lazy<bool>(StartThread, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>Igaz, ha az üzenethurok fut.</summary>
    public bool IsRunning => _started.IsValueCreated && _started.Value && Volatile.Read(ref _disposed) == 0;

    public IDisposable Register(OsNotification notification, Action handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);
            _handlers[notification] = [.. Handlers(notification), handler];
        }

        _ = _started.Value;
        return new Registration(this, notification, handler);
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed != 0)
            {
                return;
            }

            _disposed = 1;
            _handlers.Clear();
        }

        if (_started.IsValueCreated && _dispatcher is { } dispatcher)
        {
            dispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
            _thread?.Join(StopTimeout);
        }
    }

    /// <summary>A Win32 ablaküzenet → <see cref="OsNotification"/> leképezés (a demultiplexálás táblája).</summary>
    internal static OsNotification? Classify(int message, IntPtr wParam) => message switch
    {
        NativeMethods.WM_POWERBROADCAST => (int)wParam switch
        {
            NativeMethods.PBT_APMPOWERSTATUSCHANGE => OsNotification.PowerStatusChanged,
            NativeMethods.PBT_APMRESUMEAUTOMATIC or NativeMethods.PBT_APMRESUMESUSPEND => OsNotification.Resumed,
            _ => null,
        },
        NativeMethods.WM_TIMECHANGE => OsNotification.TimeChanged,
        NativeMethods.WM_DISPLAYCHANGE or NativeMethods.WM_SETTINGCHANGE or NativeMethods.WM_DPICHANGED => OsNotification.DisplayChanged,
        _ => null,
    };

    /// <summary>A WinEvent → <see cref="OsNotification"/> leképezés. Csak magukra az ablakokra figyelünk (kurzor, caret nem).</summary>
    internal static OsNotification? Classify(uint winEvent, int idObject, int idChild)
    {
        if (idObject != NativeMethods.OBJID_WINDOW || idChild != NativeMethods.CHILDID_SELF)
        {
            return null;
        }

        return winEvent switch
        {
            NativeMethods.EVENT_OBJECT_LOCATIONCHANGE or NativeMethods.EVENT_SYSTEM_MOVESIZEEND => OsNotification.WindowMoved,
            NativeMethods.EVENT_OBJECT_SHOW or NativeMethods.EVENT_OBJECT_HIDE or NativeMethods.EVENT_OBJECT_DESTROY
                or NativeMethods.EVENT_SYSTEM_MINIMIZESTART or NativeMethods.EVENT_SYSTEM_MINIMIZEEND
                or NativeMethods.EVENT_SYSTEM_FOREGROUND => OsNotification.WindowsChanged,
            _ => null,
        };
    }

    /// <summary>A regisztrált handlerek meghívása (a Reactor szálán; tesztekben közvetlenül).</summary>
    internal void Dispatch(OsNotification notification)
    {
        Action[] handlers;
        lock (_sync)
        {
            handlers = Handlers(notification);
        }

        foreach (var handler in handlers)
        {
            try
            {
                handler();
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"[OsEventEngine] Reactor handler hiba ({notification}): {ex.Message}");
            }
        }
    }

    private Action[] Handlers(OsNotification notification) =>
        _handlers.TryGetValue(notification, out var handlers) ? handlers : [];

    private void Unregister(OsNotification notification, Action handler)
    {
        lock (_sync)
        {
            if (_handlers.TryGetValue(notification, out var handlers))
            {
                _handlers[notification] = handlers.Where(h => h != handler).ToArray();
            }
        }
    }

    private bool StartThread()
    {
        using var ready = new ManualResetEventSlim();
        var thread = new Thread(() => Run(ready)) { IsBackground = true, Name = "MonkeyStack OS Reactor" };
        thread.SetApartmentState(ApartmentState.STA);
        _thread = thread;
        thread.Start();
        if (!ready.Wait(StartTimeout) || _dispatcher is null)
        {
            Trace.TraceWarning("[OsEventEngine] A Reactor nem indult el; a pollozás marad.");
            return false;
        }

        return true;
    }

    private void Run(ManualResetEventSlim ready)
    {
        try
        {
            _window = new HwndSource(new HwndSourceParameters("MonkeyStack OS Reactor") { WindowStyle = 0, Width = 0, Height = 0 });
            _window.AddHook(WndProc);
            foreach (var (min, max) in HookRanges)
            {
                var hook = NativeMethods.SetWinEventHook(
                    min, max, IntPtr.Zero, _winEventProc, 0, 0,
                    NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS);
                if (hook != IntPtr.Zero)
                {
                    _hooks.Add(hook);
                }
            }

            _dispatcher = Dispatcher.CurrentDispatcher;
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"[OsEventEngine] Reactor indítási hiba: {ex.Message}");
            Cleanup();
            ready.Set();
            return;
        }

        ready.Set();
        if (Volatile.Read(ref _disposed) == 0)
        {
            Dispatcher.Run();
        }

        Cleanup();
    }

    private void Cleanup()
    {
        foreach (var hook in _hooks)
        {
            NativeMethods.UnhookWinEvent(hook);
        }

        _hooks.Clear();
        _window?.Dispose();
        _window = null;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (Classify(msg, wParam) is OsNotification notification)
        {
            Dispatch(notification);
        }

        return IntPtr.Zero;
    }

    private void OnWinEvent(IntPtr hook, uint eventType, IntPtr hWnd, int idObject, int idChild, uint eventThread, uint eventTime)
    {
        if (Classify(eventType, idObject, idChild) is OsNotification notification)
        {
            Dispatch(notification);
        }
    }

    private sealed class Registration(Win32EventReactor owner, OsNotification notification, Action handler) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                owner.Unregister(notification, handler);
            }
        }
    }
}
