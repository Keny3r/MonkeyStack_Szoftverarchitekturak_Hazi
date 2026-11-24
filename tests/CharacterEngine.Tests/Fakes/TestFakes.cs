namespace CharacterEngine.Tests.Fakes;

using System.Windows.Media;
using System.Windows.Media.Imaging;
using MonkeyStack.Core.Animation;
using MonkeyStack.Core.Character;
using MonkeyStack.Core.Configuration;
using MonkeyStack.Core.Events;
using MonkeyStack.Core.Timing;

public class FakeAnimation : IAnimation
{
    public int Width { get; set; } = 80;
    public int Height { get; set; } = 60;
    public BitmapSource? CurrentFrame { get; set; }
    public int CurrentFrameIndex { get; set; }
    public int FrameCount { get; set; } = 1;
    public double FramesPerSecond { get; set; } = 10.0;
    public int ZIndex { get; set; }

    public FakeAnimation(int width = 80, int height = 60, int zIndex = 0)
    {
        Width = width;
        Height = height;
        ZIndex = zIndex;
        CurrentFrame = Create1x1Bitmap();
    }

    public int GetWidth() => Width;
    public int GetHeight() => Height;

    private static BitmapSource Create1x1Bitmap()
    {
        return BitmapSource.Create(
            1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 255, 0, 0, 255 }, 4);
    }
}

public class FakePhysicsModel : ICharacterPhysicsModel
{
    public double X { get; set; } = 100.0;
    public double Y { get; set; } = 200.0;
    public MovementState State { get; set; } = MovementState.Falling;
    public bool IsFacingLeft { get; set; }

    public int UpdateCallCount { get; private set; }
    public TimeSpan LastDeltaTime { get; private set; }
    public IReadOnlyList<PlatformSegment>? LastPlatforms { get; private set; }
    public double LastCursorX { get; private set; }
    public double LastCursorY { get; private set; }
    public bool LastIsMouseDown { get; private set; }
    public int LastCharacterWidth { get; private set; }
    public int LastCharacterHeight { get; private set; }

    public void Update(
        TimeSpan deltaTime,
        IReadOnlyList<PlatformSegment> platforms,
        double cursorX,
        double cursorY,
        bool isMouseDown,
        int characterWidth,
        int characterHeight)
    {
        UpdateCallCount++;
        LastDeltaTime = deltaTime;
        LastPlatforms = platforms;
        LastCursorX = cursorX;
        LastCursorY = cursorY;
        LastIsMouseDown = isMouseDown;
        LastCharacterWidth = characterWidth;
        LastCharacterHeight = characterHeight;
    }
}

public class FakeFrameLoop : IFrameLoop
{
    public event EventHandler<FrameTickEventArgs>? Tick;

    public void TriggerTick(TimeSpan deltaTime)
    {
        Tick?.Invoke(this, new FrameTickEventArgs(deltaTime));
    }
}

public class FakePlatformProvider : IPlatformProvider
{
    public PlatformSnapshot Current { get; set; } = PlatformSnapshot.Empty;
    public event EventHandler<PlatformSnapshot>? PlatformsChanged;

    public void SetPlatforms(IReadOnlyList<PlatformSegment> platforms)
    {
        Current = new PlatformSnapshot(platforms, [], Current.Version + 1);
        PlatformsChanged?.Invoke(this, Current);
    }
}

public class FakeConfigurationService : IConfigurationService
{
    public event EventHandler? ConfigurationChanged;

    public int CharacterWidth { get; set; } = 100;
    public int CharacterHeight { get; set; } = 100;

    public IAnimation? MovementAnimation { get; set; }
    public List<IAnimation> OverlayAnimations { get; set; } = new();

    public IAnimation? GetMovementAnimation(MovementState state) => MovementAnimation;

    public IReadOnlyList<IAnimation> GetOverlayAnimations(OsEventDataRegistry registry) => OverlayAnimations;

    public void TriggerConfigurationChanged() => ConfigurationChanged?.Invoke(this, EventArgs.Empty);
}

public class FakeOsEventNotifier : IOsEventNotifier
{
    public event EventHandler<CpuEventData>? CpuChanged;
    public event EventHandler<MemoryEventData>? MemoryChanged;
    public event EventHandler<DiskEventData>? DiskChanged;
    public event EventHandler<BatteryEventData>? BatteryChanged;
    public event EventHandler<WindowCountEventData>? WindowCountChanged;
    public event EventHandler<TimeEventData>? TimeChanged;
    public event EventHandler<InactivityEventData>? InactivityChanged;

    public void TriggerCpuChanged(int usagePercent) =>
        CpuChanged?.Invoke(this, new CpuEventData { UsagePercent = usagePercent });

    public void TriggerMemoryChanged(int usagePercent) =>
        MemoryChanged?.Invoke(this, new MemoryEventData { UsagePercent = usagePercent });

    public void TriggerDiskChanged(int usagePercent) =>
        DiskChanged?.Invoke(this, new DiskEventData { UsagePercent = usagePercent });

    public void TriggerBatteryChanged(int chargePercent) =>
        BatteryChanged?.Invoke(this, new BatteryEventData { ChargePercent = chargePercent });

    public void TriggerWindowCountChanged(int windowCount) =>
        WindowCountChanged?.Invoke(this, new WindowCountEventData { WindowCount = windowCount });

    public void TriggerTimeChanged(int hour, int minute) =>
        TimeChanged?.Invoke(this, new TimeEventData { Hour = hour, Minute = minute });

    public void TriggerInactivityChanged(int minutes) =>
        InactivityChanged?.Invoke(this, new InactivityEventData { InactivityMinutes = minutes });
}
