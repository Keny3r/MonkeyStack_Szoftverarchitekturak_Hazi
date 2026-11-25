namespace OsEventEngine.Platforms;

using MonkeyStack.Core.Character;
using OsEventEngine.Native;

/// <summary>
/// A járható felületek kiszámítása egy <see cref="DesktopScene"/>-ből. Tiszta függvény, natív hívás nélkül
/// (docs/os_event_engine/platform_provider_proposal.md, 3. fejezet):
/// <list type="number">
/// <item>Minden ablak felső éle platform, kivéve a maximalizált, a teljes képernyős és a munkaterület tetejéig érő ablakot.</item>
/// <item>Takarás: az élből kivonjuk az előtte lévő (Z-sorrend) ablakok vízszintes tartományát, ha azok lefedik az él magasságát.
/// A maximalizált és a teljes képernyős ablak is takar, ezért az alatta lévő ablakok elveszítik a platformjukat, és a karakter leesik.</item>
/// <item>Talaj monitoronként: a munkaterület alja (alul lévő tálcánál a tálca teteje), teljes képernyős ablaknál a monitor alja.</item>
/// <item>A szakaszokat monitoronként a munkaterületre vágjuk, és a monitor DPI-jével WPF egységre (DIP) váltjuk.</item>
/// <item>A <see cref="MinPlatformWidth"/>-nél keskenyebb szakaszokat eldobjuk (a #3 döntése).</item>
/// </list>
/// </summary>
internal static class PlatformCalculator
{
    /// <summary>A legkeskenyebb megtartott szakasz DIP-ben (2 × 10 px szélső ráhagyás a fizikában).</summary>
    public const double MinPlatformWidth = 20;

    public static (IReadOnlyList<PlatformSegment> Platforms, IReadOnlyList<ScreenArea> Screens) Calculate(DesktopScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        var monitors = scene.Monitors;
        var windows = scene.Windows;

        var fullscreen = monitors.Select(m => windows.Any(w => Covers(w.Bounds, m.Bounds))).ToArray();
        var platforms = new List<PlatformSegment>();

        for (int i = 0; i < windows.Count; i++)
        {
            var window = windows[i].Bounds;
            int own = OwnMonitor(window, monitors);
            if (own < 0 || !HasOwnPlatform(windows[i], monitors[own]))
            {
                continue;
            }

            var visible = new List<(double Left, double Right)> { (window.Left, window.Right) };
            for (int j = 0; j < i && visible.Count > 0; j++)
            {
                var front = windows[j].Bounds;
                if (front.Top <= window.Top && window.Top < front.Bottom)
                {
                    visible = Subtract(visible, front.Left, front.Right);
                }
            }

            foreach (var (left, right) in visible)
            {
                for (int m = 0; m < monitors.Count; m++)
                {
                    AddClipped(platforms, monitors[m], fullscreen[m], window.Top, left, right);
                }
            }
        }

        var screens = new List<ScreenArea>(monitors.Count);
        for (int m = 0; m < monitors.Count; m++)
        {
            var monitor = monitors[m];
            var groundArea = fullscreen[m] ? monitor.Bounds : monitor.WorkArea;
            AddIfWide(platforms, ToDip(groundArea.Bottom, groundArea.Left, groundArea.Right, monitor.Scale));
            screens.Add(new ScreenArea(ToDip(monitor.Bounds, monitor.Scale), ToDip(monitor.WorkArea, monitor.Scale), fullscreen[m]));
        }

        platforms.Sort(static (a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.LeftX.CompareTo(b.LeftX));
        return (platforms, screens);
    }

    /// <summary>Két kiszámított eredmény azonos-e (a felesleges <c>PlatformsChanged</c> elkerülésére).</summary>
    public static bool SameAs(PlatformSnapshot snapshot, IReadOnlyList<PlatformSegment> platforms, IReadOnlyList<ScreenArea> screens) =>
        snapshot.Platforms.SequenceEqual(platforms) && snapshot.Screens.SequenceEqual(screens);

    private static bool HasOwnPlatform(SceneWindow window, SceneMonitor monitor) =>
        !window.IsMaximized
        && !Covers(window.Bounds, monitor.Bounds)
        && window.Bounds.Top > monitor.WorkArea.Top;

    private static void AddClipped(List<PlatformSegment> platforms, SceneMonitor monitor, bool fullscreen, double y, double left, double right)
    {
        double bottom = fullscreen ? monitor.Bounds.Bottom : monitor.WorkArea.Bottom;
        if (y < monitor.WorkArea.Top || y >= bottom)
        {
            return;
        }

        double clippedLeft = Math.Max(left, monitor.WorkArea.Left);
        double clippedRight = Math.Min(right, monitor.WorkArea.Right);
        if (clippedRight > clippedLeft)
        {
            AddIfWide(platforms, ToDip(y, clippedLeft, clippedRight, monitor.Scale));
        }
    }

    private static void AddIfWide(List<PlatformSegment> platforms, PlatformSegment segment)
    {
        if (segment.Width >= MinPlatformWidth)
        {
            platforms.Add(segment);
        }
    }

    /// <summary>Az ablakot a legnagyobb közös területű monitorhoz rendeljük (mint a <c>MonitorFromWindow</c>); -1, ha egyikkel sem fed át.</summary>
    private static int OwnMonitor(ScreenRect window, IReadOnlyList<SceneMonitor> monitors)
    {
        int best = -1;
        double bestArea = 0;
        for (int m = 0; m < monitors.Count; m++)
        {
            var b = monitors[m].Bounds;
            double width = Math.Min(window.Right, b.Right) - Math.Max(window.Left, b.Left);
            double height = Math.Min(window.Bottom, b.Bottom) - Math.Max(window.Top, b.Top);
            double area = width > 0 && height > 0 ? width * height : 0;
            if (area > bestArea)
            {
                bestArea = area;
                best = m;
            }
        }

        return best;
    }

    private static bool Covers(ScreenRect outer, ScreenRect inner) =>
        outer.Left <= inner.Left && outer.Top <= inner.Top && outer.Right >= inner.Right && outer.Bottom >= inner.Bottom;

    private static List<(double Left, double Right)> Subtract(List<(double Left, double Right)> intervals, double left, double right)
    {
        var result = new List<(double Left, double Right)>(intervals.Count + 1);
        foreach (var (l, r) in intervals)
        {
            if (right <= l || left >= r)
            {
                result.Add((l, r));
                continue;
            }

            if (left > l)
            {
                result.Add((l, left));
            }

            if (right < r)
            {
                result.Add((right, r));
            }
        }

        return result;
    }

    private static PlatformSegment ToDip(double y, double left, double right, double scale) =>
        new(y / scale, left / scale, right / scale);

    private static ScreenRect ToDip(ScreenRect r, double scale) =>
        new(r.Left / scale, r.Top / scale, r.Right / scale, r.Bottom / scale);
}
