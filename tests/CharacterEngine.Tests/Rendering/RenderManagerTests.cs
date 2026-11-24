namespace CharacterEngine.Tests.Rendering;

using CharacterEngine.Rendering;
using CharacterEngine.Tests.Fakes;
using MonkeyStack.Core.Character;

public class RenderManagerTests
{
    [Fact]
    public void Constructor_PublishesInitialSnapshot()
    {
        var physics = new FakePhysicsModel { X = 150, Y = 250, IsFacingLeft = true };
        var configService = new FakeConfigurationService
        {
            MovementAnimation = new FakeAnimation(64, 64)
        };
        var osNotifier = new FakeOsEventNotifier();
        var coordinator = new CharacterEventCoordinator(configService, osNotifier);

        var manager = new RenderManager(physics, coordinator);

        var snapshot = manager.LatestSnapshot;
        Assert.Equal(150, snapshot.X);
        Assert.Equal(250, snapshot.Y);
        Assert.True(snapshot.IsFacingLeft);
        Assert.Same(configService.MovementAnimation, snapshot.MovementAnimation);
        Assert.True(snapshot.IsVisible);
    }

    [Fact]
    public void StepSimulation_AdvancesPhysics_AndUpdatesSnapshot()
    {
        var physics = new FakePhysicsModel { X = 100, Y = 200 };
        var configService = new FakeConfigurationService
        {
            MovementAnimation = new FakeAnimation(80, 60)
        };
        var osNotifier = new FakeOsEventNotifier();
        var coordinator = new CharacterEventCoordinator(configService, osNotifier);
        var manager = new RenderManager(physics, coordinator);

        var platforms = new List<PlatformSegment>
        {
            new(Y: 300, LeftX: 0, RightX: 500)
        };

        var snapshot = manager.StepSimulation(
            TimeSpan.FromMilliseconds(16.6),
            overridePlatforms: platforms,
            overrideCursorX: 120,
            overrideCursorY: 180,
            overrideIsMouseDown: true);

        Assert.Equal(1, physics.UpdateCallCount);
        Assert.Equal(TimeSpan.FromMilliseconds(16.6), physics.LastDeltaTime);
        Assert.Same(platforms, physics.LastPlatforms);
        Assert.Equal(120, physics.LastCursorX);
        Assert.Equal(180, physics.LastCursorY);
        Assert.True(physics.LastIsMouseDown);
        Assert.Equal(80, physics.LastCharacterWidth);
        Assert.Equal(60, physics.LastCharacterHeight);

        Assert.Equal(100, snapshot.X);
        Assert.Equal(200, snapshot.Y);
    }

    [Fact]
    public void StepSimulation_WhenPhysicsStateChanges_UpdatesCoordinator()
    {
        var physics = new FakePhysicsModel { State = MovementState.Falling };
        var configService = new FakeConfigurationService();
        var osNotifier = new FakeOsEventNotifier();
        var coordinator = new CharacterEventCoordinator(configService, osNotifier);
        var manager = new RenderManager(physics, coordinator);

        Assert.Equal(MovementState.Falling, coordinator.CurrentMovementState);

        // A fizika állapotot vált
        physics.State = MovementState.Walking;

        manager.StepSimulation(TimeSpan.FromMilliseconds(16));

        Assert.Equal(MovementState.Walking, coordinator.CurrentMovementState);
        Assert.Equal(MovementState.Walking, physics.State);
    }

    [Fact]
    public void StepSimulation_ReadsFromPlatformProviderAndInputProvider()
    {
        var physics = new FakePhysicsModel();
        var configService = new FakeConfigurationService();
        var osNotifier = new FakeOsEventNotifier();
        var coordinator = new CharacterEventCoordinator(configService, osNotifier);

        var platformProvider = new FakePlatformProvider();
        var platforms = new List<PlatformSegment> { new(Y: 500, LeftX: 10, RightX: 200) };
        platformProvider.SetPlatforms(platforms);

        var manager = new RenderManager(
            physics,
            coordinator,
            platformProvider: platformProvider,
            inputProvider: () => (cursorX: 350.0, cursorY: 420.0, isMouseDown: false));

        manager.StepSimulation(TimeSpan.FromMilliseconds(16));

        Assert.Equal(platforms, physics.LastPlatforms);
        Assert.Equal(350.0, physics.LastCursorX);
        Assert.Equal(420.0, physics.LastCursorY);
        Assert.False(physics.LastIsMouseDown);
    }

    [Fact]
    public void AttachToFrameLoop_AdvancesSimulationOnTick()
    {
        var physics = new FakePhysicsModel();
        var configService = new FakeConfigurationService();
        var osNotifier = new FakeOsEventNotifier();
        var coordinator = new CharacterEventCoordinator(configService, osNotifier);
        var frameLoop = new FakeFrameLoop();

        var manager = new RenderManager(physics, coordinator);
        manager.AttachToFrameLoop(frameLoop);

        Assert.Equal(0, physics.UpdateCallCount);

        frameLoop.TriggerTick(TimeSpan.FromMilliseconds(16.7));
        Assert.Equal(1, physics.UpdateCallCount);

        frameLoop.TriggerTick(TimeSpan.FromMilliseconds(16.7));
        Assert.Equal(2, physics.UpdateCallCount);

        manager.DetachFromFrameLoop();
        frameLoop.TriggerTick(TimeSpan.FromMilliseconds(16.7));
        Assert.Equal(2, physics.UpdateCallCount);
    }

    [Fact]
    public async Task LatestSnapshot_IsThreadSafeUnderConcurrentAccess()
    {
        var physics = new FakePhysicsModel();
        var configService = new FakeConfigurationService();
        var osNotifier = new FakeOsEventNotifier();
        var coordinator = new CharacterEventCoordinator(configService, osNotifier);
        var manager = new RenderManager(physics, coordinator);

        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var token = cancellationTokenSource.Token;

        // Háttérszál: folyamatosan lépteti a szimulációt
        var simulationTask = Task.Run(() =>
        {
            double x = 0;
            while (!token.IsCancellationRequested)
            {
                physics.X = x++;
                physics.Y = x * 2;
                manager.StepSimulation(TimeSpan.FromMilliseconds(16));
            }
        }, CancellationToken.None);

        // UI szál szimuláció: folyamatosan olvassa a LatestSnapshot-ot
        var readingTask = Task.Run(() =>
        {
            while (!token.IsCancellationRequested)
            {
                var snapshot = manager.LatestSnapshot;
                Assert.NotNull(snapshot);
                Assert.True(snapshot.IsVisible);
            }
        }, CancellationToken.None);

        await Task.WhenAll(simulationTask, readingTask);
    }

    [Fact]
    public void Dispose_DetachesSubscriptionsAndRenderer()
    {
        var physics = new FakePhysicsModel();
        var configService = new FakeConfigurationService();
        var osNotifier = new FakeOsEventNotifier();
        var coordinator = new CharacterEventCoordinator(configService, osNotifier);
        var frameLoop = new FakeFrameLoop();

        var manager = new RenderManager(physics, coordinator);
        manager.AttachToFrameLoop(frameLoop);

        manager.Dispose();

        // Léptetés Dispose után már nem hívja a fizikát
        frameLoop.TriggerTick(TimeSpan.FromMilliseconds(16));
        Assert.Equal(0, physics.UpdateCallCount);
    }
}
