namespace OsEventEngine.Tests;

using Microsoft.Extensions.Time.Testing;
using MonkeyStack.Core.Events;
using OsEventEngine.Native;
using OsEventEngine.Notifications;
using OsEventEngine.Sources;

/// <summary>
/// A Reactor (M9) demultiplexálási táblája és a bekötése a monitorokba, natív hook nélkül.
/// </summary>
public class OsNotificationTests
{
    [Theory]
    [InlineData(NativeMethods.EVENT_OBJECT_SHOW, OsNotification.WindowsChanged)]
    [InlineData(NativeMethods.EVENT_OBJECT_HIDE, OsNotification.WindowsChanged)]
    [InlineData(NativeMethods.EVENT_OBJECT_DESTROY, OsNotification.WindowsChanged)]
    [InlineData(NativeMethods.EVENT_SYSTEM_MINIMIZESTART, OsNotification.WindowsChanged)]
    [InlineData(NativeMethods.EVENT_SYSTEM_FOREGROUND, OsNotification.WindowsChanged)]
    [InlineData(NativeMethods.EVENT_OBJECT_LOCATIONCHANGE, OsNotification.WindowMoved)]
    [InlineData(NativeMethods.EVENT_SYSTEM_MOVESIZEEND, OsNotification.WindowMoved)]
    internal void WinEventsOfWindowsAreClassified(uint winEvent, OsNotification expected)
    {
        Assert.Equal(expected, Win32EventReactor.Classify(winEvent, NativeMethods.OBJID_WINDOW, NativeMethods.CHILDID_SELF));
    }

    [Fact]
    public void WinEventsOfOtherObjectsAreIgnored()
    {
        const int OBJID_CURSOR = -9;

        Assert.Null(Win32EventReactor.Classify(NativeMethods.EVENT_OBJECT_LOCATIONCHANGE, OBJID_CURSOR, NativeMethods.CHILDID_SELF));
        Assert.Null(Win32EventReactor.Classify(NativeMethods.EVENT_OBJECT_SHOW, NativeMethods.OBJID_WINDOW, 3));
    }

    [Theory]
    [InlineData(NativeMethods.WM_POWERBROADCAST, NativeMethods.PBT_APMPOWERSTATUSCHANGE, OsNotification.PowerStatusChanged)]
    [InlineData(NativeMethods.WM_POWERBROADCAST, NativeMethods.PBT_APMRESUMEAUTOMATIC, OsNotification.Resumed)]
    [InlineData(NativeMethods.WM_TIMECHANGE, 0, OsNotification.TimeChanged)]
    [InlineData(NativeMethods.WM_DISPLAYCHANGE, 0, OsNotification.DisplayChanged)]
    [InlineData(NativeMethods.WM_SETTINGCHANGE, 0x2F, OsNotification.DisplayChanged)]
    internal void WindowMessagesAreClassified(int message, int wParam, OsNotification expected)
    {
        Assert.Equal(expected, Win32EventReactor.Classify(message, wParam));
    }

    [Fact]
    public void UnrelatedWindowMessagesAreIgnored()
    {
        const int WM_PAINT = 0x000F;

        Assert.Null(Win32EventReactor.Classify(WM_PAINT, IntPtr.Zero));
        Assert.Null(Win32EventReactor.Classify(NativeMethods.WM_POWERBROADCAST, 0x0004)); // PBT_APMSUSPEND
    }

    [Fact]
    public void ThrottleMergesBurstsIntoOneRun()
    {
        var time = new FakeTimeProvider();
        int runs = 0;
        using var throttle = new Throttle(TimeSpan.FromMilliseconds(200), time, () => runs++);

        for (int i = 0; i < 10; i++)
        {
            throttle.Signal();
        }

        time.Advance(TimeSpan.FromMilliseconds(199));
        Assert.Equal(0, runs);
        time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(1, runs);

        throttle.Signal();
        time.Advance(TimeSpan.FromMilliseconds(200));
        Assert.Equal(2, runs);
    }

    [Fact]
    public async Task WindowNotificationTriggersAnImmediateWindowCountSample()
    {
        var (service, time, notifications, windows) = CreateService(OsEventEngineOptions.Default);
        using (service)
        {
            service.Start(context: null);
            Assert.True(await Eventually.UntilAsync(() => windows.Calls >= 1));
            int before = windows.Calls;

            notifications.Raise(OsNotification.WindowsChanged);
            notifications.Raise(OsNotification.WindowsChanged);
            time.Advance(TimeSpan.FromMilliseconds(200));

            Assert.True(await Eventually.UntilAsync(() => windows.Calls == before + 1));
        }
    }

    [Fact]
    public async Task NotificationsCanBeTurnedOff()
    {
        var (service, time, notifications, windows) = CreateService(OsEventEngineOptions.Default with { EnableOsNotifications = false });
        using (service)
        {
            service.Start(context: null);
            Assert.True(await Eventually.UntilAsync(() => windows.Calls >= 1));
            int before = windows.Calls;

            notifications.Raise(OsNotification.WindowsChanged);
            time.Advance(TimeSpan.FromMilliseconds(200));
            await Task.Delay(100);

            Assert.Equal(before, windows.Calls);
        }
    }

    [Fact]
    public void DisposeUnregistersFromTheReactor()
    {
        var (service, _, notifications, _) = CreateService(OsEventEngineOptions.Default);
        service.Start(context: null);
        Assert.Equal(1, notifications.Count(OsNotification.TimeChanged));

        service.Dispose();

        Assert.Equal(0, notifications.Count(OsNotification.TimeChanged));
    }

    private static (OsEventMonitorService Service, FakeTimeProvider Time, FakeNotifications Notifications, FakeSource Windows) CreateService(
        OsEventEngineOptions options)
    {
        var time = new FakeTimeProvider();
        var notifications = new FakeNotifications();
        var windows = new FakeSource(Metric.WindowCount, 5);

        // Csak az ablakszám fut, hosszú periódussal, hogy a mintavételt egyértelműen az értesítés váltsa ki.
        var monitors = options.Monitors.ToDictionary(
            m => m.Key,
            m => m.Value with { Enabled = m.Key == Metric.WindowCount, Interval = TimeSpan.FromMinutes(1) });
        var service = new OsEventMonitorService(
            new OsEventNotifier(),
            options with { Monitors = monitors },
            time,
            _ => [windows],
            () => notifications);
        return (service, time, notifications, windows);
    }
}
