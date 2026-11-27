namespace CharacterEngine.Tests.Fakes;

using MonkeyStack.Core.Character;

/// <summary>
/// Monitorlistát adó hamis platform-szolgáltató az overlay ablak tesztjeihez (a <see cref="FakePlatformProvider"/> csak
/// platformokat állít, monitorokat nem).
/// </summary>
public class FakeScreenProvider : IPlatformProvider
{
    public PlatformSnapshot Current { get; private set; } = PlatformSnapshot.Empty;

    public event EventHandler<PlatformSnapshot>? PlatformsChanged;

    public int SubscriberCount => PlatformsChanged?.GetInvocationList().Length ?? 0;

    public void SetScreens(params ScreenRect[] screens)
    {
        var areas = screens.Select(s => new ScreenArea(s, s with { Bottom = s.Bottom - 40 }, HasFullscreenWindow: false)).ToList();
        Current = new PlatformSnapshot([], areas, Current.Version + 1);
        PlatformsChanged?.Invoke(this, Current);
    }
}
