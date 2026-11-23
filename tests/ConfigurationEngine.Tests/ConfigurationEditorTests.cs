namespace ConfigurationEngine.Tests;

using ConfigurationEngine.Media;
using ConfigurationEngine.Model;
using ConfigurationEngine.Storage;
using ConfigurationEngine.Validation;
using MonkeyStack.Core.Character;

/// <summary>
/// A <see cref="ConfigurationService"/> mint <see cref="IConfigurationEditor"/> (view.md, "Kapcsolat a modellel") és a
/// közös léptetőről (<see cref="MonkeyStack.Core.Timing.IFrameLoop"/>) futó léptetés.
/// </summary>
public sealed class ConfigurationEditorTests : IDisposable
{
    private static readonly AnimationSource Walk = new() { Id = 1, Name = "Séta", SourceType = AnimationSourceType.Gif, Path = "walk.gif" };

    private readonly FakeStore _store = new() { Data = TestData.ValidConfiguration() };
    private readonly FakeFrameLoader _loader = new();
    private readonly FakeMediaValidator _media = new();
    private readonly ManualFrameLoop _frameLoop = new();
    private readonly ConfigurationService _service;

    public ConfigurationEditorTests()
    {
        _loader.FrameCounts["walk.gif"] = 6;
        _service = new ConfigurationService(_store, _loader, _media, _frameLoop);
    }

    private IConfigurationEditor Editor => _service;

    public void Dispose() => _service.Dispose();

    [Fact]
    public async Task FrameLoopTick_StepsRunningAnimationsAndPreviews()
    {
        await _service.InitializeAsync();
        var preview = await Editor.CreatePreviewAnimationAsync(Walk, framesPerSecond: 4);

        _frameLoop.Step(TimeSpan.FromSeconds(0.25));

        Assert.Equal(3, _service.GetMovementAnimation(MovementState.Walking)!.CurrentFrameIndex); // 12 fps × 0,25 s
        Assert.Equal(2, _service.GetMovementAnimation(MovementState.Idle)!.CurrentFrameIndex); // 8 fps × 0,25 s
        Assert.Equal(1, preview.CurrentFrameIndex); // 4 fps × 0,25 s
    }

    [Fact]
    public void Dispose_UnsubscribesFromFrameLoop()
    {
        Assert.True(_frameLoop.HasSubscribers);

        _service.Dispose();

        Assert.False(_frameLoop.HasSubscribers);
    }

    [Fact]
    public async Task Previews_OfSameSource_ShareFrames()
    {
        var slow = await Editor.CreatePreviewAnimationAsync(Walk, framesPerSecond: 2);
        var fast = await Editor.CreatePreviewAnimationAsync(Walk.Copy(), framesPerSecond: 20);

        Assert.Equal(1, _loader.CallsFor("walk.gif"));
        Assert.Same(slow.CurrentFrame, fast.CurrentFrame);
        Assert.Equal((2, 20), (slow.FramesPerSecond, fast.FramesPerSecond));
        Assert.Equal((_service.CharacterWidth, _service.CharacterHeight), (slow.GetWidth(), slow.GetHeight()));
    }

    [Fact]
    public async Task ReleaseThenCreate_OfSameSource_ReusesFrames()
    {
        var slow = await Editor.CreatePreviewAnimationAsync(Walk, framesPerSecond: 2);

        // A View sebességváltáskor előbb eldobja a régi előnézetet, és csak utána kér újat.
        Editor.ReleasePreviewAnimation(slow);
        await Editor.CreatePreviewAnimationAsync(Walk, framesPerSecond: 8);

        Assert.Equal(1, _loader.CallsFor("walk.gif"));
    }

    [Fact]
    public async Task CancelledRequest_LeavesSharedLoadForTheNextRequest()
    {
        _loader.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancel = new CancellationTokenSource();
        var cancelled = Editor.CreatePreviewAnimationAsync(Walk, framesPerSecond: 2, cancel.Token);

        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
        _loader.Gate.SetResult();
        await Editor.CreatePreviewAnimationAsync(Walk, framesPerSecond: 8);

        Assert.Equal(1, _loader.CallsFor("walk.gif"));
    }

    [Fact]
    public async Task UnusedFrames_AreDroppedWhenAnotherPreviewIsCreated()
    {
        var walk = await Editor.CreatePreviewAnimationAsync(Walk, framesPerSecond: 4);

        Editor.ReleasePreviewAnimation(walk);
        await Editor.CreatePreviewAnimationAsync(Run(), framesPerSecond: 4);
        await Editor.CreatePreviewAnimationAsync(Walk, framesPerSecond: 4);

        Assert.Equal(2, _loader.CallsFor("walk.gif"));
    }

    [Fact]
    public async Task ReleasedPreview_IsNoLongerStepped_OthersAre()
    {
        var released = await Editor.CreatePreviewAnimationAsync(Walk, framesPerSecond: 4);
        var kept = await Editor.CreatePreviewAnimationAsync(Walk, framesPerSecond: 4);

        Editor.ReleasePreviewAnimation(released);
        _frameLoop.Step(TimeSpan.FromSeconds(0.5));

        Assert.Equal(0, released.CurrentFrameIndex);
        Assert.Equal(2, kept.CurrentFrameIndex);
        Assert.Equal(1, _service.PreviewCount);
    }

    [Fact]
    public async Task ReleaseAll_StopsEveryPreview_AndReloadsFramesForNewOnes()
    {
        var preview = await Editor.CreatePreviewAnimationAsync(Walk, framesPerSecond: 4);

        Editor.ReleasePreviewAnimations();
        _frameLoop.Step(TimeSpan.FromSeconds(0.5));
        await Editor.CreatePreviewAnimationAsync(Walk, framesPerSecond: 4);

        Assert.Equal(0, preview.CurrentFrameIndex);
        Assert.Equal(1, _service.PreviewCount);
        Assert.Equal(2, _loader.CallsFor("walk.gif"));
    }

    [Fact]
    public async Task Reload_KeepsPreviews()
    {
        await _service.InitializeAsync();
        var preview = await Editor.CreatePreviewAnimationAsync(Walk, framesPerSecond: 4);
        var changed = WaitForChange();

        _store.RaiseChanged();
        await changed;
        _frameLoop.Step(TimeSpan.FromSeconds(0.5));

        Assert.Equal(1, _service.PreviewCount);
        Assert.Equal(2, preview.CurrentFrameIndex);
    }

    [Fact]
    public async Task SetCharacterSize_ResizesPreviewsAtOnce_ThenLoadsTheirFramesAtNewSize()
    {
        var preview = await Editor.CreatePreviewAnimationAsync(Walk, framesPerSecond: 4);

        Editor.SetCharacterSize(200, 150);

        Assert.Equal((200, 150), (preview.GetWidth(), preview.GetHeight()));
        await WaitUntil(() => preview.CurrentFrame?.PixelWidth == 200);
        Assert.Equal(150, preview.CurrentFrame!.PixelHeight);
    }

    [Fact]
    public async Task FailingSource_Throws_AndIsRetriedNextTime()
    {
        _loader.Failing.Add("walk.gif");

        await Assert.ThrowsAsync<MediaLoadException>(() => Editor.CreatePreviewAnimationAsync(Walk, framesPerSecond: 4));
        _loader.Failing.Clear();
        var preview = await Editor.CreatePreviewAnimationAsync(Walk, framesPerSecond: 4);

        Assert.NotNull(preview.CurrentFrame);
        Assert.Equal(1, _service.PreviewCount);
    }

    [Fact]
    public async Task CreatePreview_AfterDispose_Throws()
    {
        _service.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => Editor.CreatePreviewAnimationAsync(Walk, framesPerSecond: 4));
    }

    [Fact]
    public async Task Load_ReturnsIndependentCopies()
    {
        var first = (await Editor.LoadAsync()).Data;
        first.BehaviorRules.Clear();

        var second = (await Editor.LoadAsync()).Data;

        Assert.Equal(3, second.BehaviorRules.Count);
    }

    [Fact]
    public void Validate_NormalizesLikeSave_AndLeavesInputUnchanged()
    {
        var data = TestData.ValidConfiguration();
        data.AnimationSources[1].Name = "  Séta ";

        var result = Editor.Validate(data);

        Assert.Contains(result.Errors, e => e.Kind == ConfigurationItemKind.AnimationSource && e.PropertyName == nameof(AnimationSource.Name));
        Assert.Equal("  Séta ", data.AnimationSources[1].Name);
    }

    [Fact]
    public async Task ValidateMedia_RunsFullCheck()
    {
        _media.Rejected.Add("walk.gif");

        var errors = await Editor.ValidateMediaAsync(Walk);

        Assert.Equal("1", Assert.Single(errors).ItemKey);
        Assert.Equal([MediaValidationDepth.Full], _media.Depths);
    }

    [Fact]
    public async Task Save_WithFailingMedia_Throws_AndStoresNothing()
    {
        var data = TestData.ValidConfiguration();
        _media.Rejected.Add("fire.png");

        var ex = await Assert.ThrowsAsync<ConfigurationValidationException>(() => Editor.SaveAsync(data));

        Assert.Equal("2", Assert.Single(ex.Result.Errors).ItemKey);
        Assert.Equal(0, _store.Saves);
    }

    [Fact]
    public async Task Save_WithInvalidConfiguration_Throws_AndStoresNothing()
    {
        var data = TestData.ValidConfiguration();
        data.Character.CharacterWidth = 0;

        await Assert.ThrowsAsync<ConfigurationValidationException>(() => Editor.SaveAsync(data));

        Assert.Equal(0, _store.Saves);
    }

    [Fact]
    public async Task Save_ChecksEverySourceFully_StoresAndReloads()
    {
        await _service.InitializeAsync();
        var data = TestData.ValidConfiguration();
        data.Character.CharacterWidth = 64;
        var changed = WaitForChange();

        await Editor.SaveAsync(data);
        await changed;

        Assert.Equal(1, _store.Saves);
        Assert.Equal(Enumerable.Repeat(MediaValidationDepth.Full, 3), _media.Depths);
        Assert.Equal(64, _service.CharacterWidth);
    }

    [Fact]
    public async Task ReadImageSize_ComesFromMediaValidator()
    {
        _media.ImageSizes["fire.png"] = new PixelSize(128, 64);
        var fire = TestData.ValidConfiguration().AnimationSources[1];

        Assert.Equal(new PixelSize(128, 64), await Editor.ReadImageSizeAsync(fire));
        await Assert.ThrowsAsync<MediaLoadException>(() => Editor.ReadImageSizeAsync(Walk));
    }

    [Fact]
    public async Task MediaLoadFailures_AreVisibleToTheEditor()
    {
        _loader.Failing.Add("pyjama");

        await _service.InitializeAsync();

        Assert.Equal(3, Assert.Single(Editor.MediaLoadFailures).AnimationSourceId);
    }

    private Task WaitForChange()
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _service.ConfigurationChanged += (_, _) => tcs.TrySetResult();
        return tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    private static AnimationSource Run() =>
        new() { Id = 2, Name = "Futás", SourceType = AnimationSourceType.Gif, Path = "run.gif" };

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "A feltétel nem teljesült időben.");
            await Task.Delay(10);
        }
    }
}
