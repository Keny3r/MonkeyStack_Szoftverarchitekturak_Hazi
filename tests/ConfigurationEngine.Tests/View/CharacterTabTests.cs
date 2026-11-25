namespace ConfigurationEngine.Tests.View;

using ConfigurationEngine.Model;
using ConfigurationEngine.View;
using ConfigurationEngine.View.Fakes;
using ConfigurationEngine.View.Tabs;
using MonkeyStack.Core.Character;

/// <summary>
/// A „Karakter” fül (view.md, "1. Karakter mérete" és "2. Mozgási animációk").
/// </summary>
public sealed class CharacterTabTests : IDisposable
{
    private readonly FakeConfigurationEditor _editor = new();
    private readonly ConfigurationWindowViewModel _viewModel;

    public CharacterTabTests()
    {
        _viewModel = new ConfigurationWindowViewModel(_editor, new AcceptingDialogs());
    }

    private CharacterTabViewModel Tab => _viewModel.CharacterTab;

    private CharacterSizeViewModel Size => Tab.Size!;

    public void Dispose() => _editor.Dispose();

    [Fact]
    public async Task Load_ShowsSizeAndFourFixedRowsInMovementStateOrder()
    {
        await _viewModel.LoadAsync();

        Assert.Equal("100", Size.Width.Text);
        Assert.Equal("100", Size.Height.Text);
        Assert.Equal(
            [MovementState.Idle, MovementState.Walking, MovementState.Falling, MovementState.Dragging],
            Tab.MovementRows.Select(r => r.State));
        Assert.Equal(_viewModel.Data!.AnimationSources.Count, Tab.SourceOptions.Count);
    }

    [Fact]
    public async Task ValidSize_IsAppliedImmediatelyButNotStored()
    {
        await _viewModel.LoadAsync();

        Size.Width.Text = "200";

        Assert.Equal((200, 100), _editor.CharacterSize);
        Assert.True(_viewModel.IsDirty);
        Assert.Equal(0, _editor.SaveCount);
    }

    [Theory]
    [InlineData("600")]
    [InlineData("8")]
    [InlineData("")]
    [InlineData("nagy")]
    public async Task InvalidSize_IsNotAppliedAndTheFieldShowsAnError(string text)
    {
        await _viewModel.LoadAsync();

        Size.Width.Text = text;

        Assert.Equal((100, 100), _editor.CharacterSize);
        Assert.True(Size.Width.HasErrors);
        Assert.NotEmpty(Size.Width.ErrorText);
        Assert.Equal("Karakter (1)", _viewModel.CharacterTabHeader);
        Assert.False(_viewModel.CanSave);
    }

    [Fact]
    public async Task AfterInvalidValue_TheLastValidSizeStaysInEffect()
    {
        await _viewModel.LoadAsync();

        Size.Width.Text = "150";
        Size.Width.Text = "1500";

        Assert.Equal((150, 100), _editor.CharacterSize);
    }

    [Fact]
    public async Task SourceChange_GoesIntoTheCopyAndEmptySelectionIsIgnored()
    {
        await _viewModel.LoadAsync();
        var walking = Tab.MovementRows.Single(r => r.State == MovementState.Walking);

        walking.AnimationSourceId = 4;
        walking.AnimationSourceId = null;

        Assert.Equal(4, walking.Movement.AnimationSourceId);
        Assert.True(_viewModel.IsDirty);
    }

    [Fact]
    public async Task RowsShowLivePreviewsOfTheEditedState()
    {
        await _viewModel.LoadAsync();
        await WaitForPreviewsAsync();
        var idle = Tab.MovementRows[0];

        idle.FramesPerSecond.Text = "24";
        await WaitForPreviewsAsync();

        Assert.Equal(24, idle.Preview.Animation!.FramesPerSecond);
        Assert.Equal(ViewTestHelpers.TabPreviews(_viewModel).Count, _editor.PreviewCount);
    }

    [Fact]
    public async Task OutOfRangeSpeed_KeepsTheLastValidPreview()
    {
        await _viewModel.LoadAsync();
        await WaitForPreviewsAsync();
        var idle = Tab.MovementRows[0];
        var before = idle.Preview.Animation;

        idle.FramesPerSecond.Text = "0";

        Assert.True(idle.FramesPerSecond.HasErrors);
        Assert.True(idle.HasAnyError);
        Assert.Same(before, idle.Preview.Animation);
    }

    [Fact]
    public async Task GoToFirstError_FocusesTheFailingMovementField()
    {
        await _viewModel.LoadAsync();
        var falling = Tab.MovementRows.Single(r => r.State == MovementState.Falling);
        falling.FramesPerSecond.Text = "99";

        _viewModel.GoToFirstErrorCommand.Execute(null);

        Assert.Equal(ConfigurationWindowViewModel.CharacterTabIndex, _viewModel.SelectedTabIndex);
        Assert.Equal(nameof(MovementAnimation.FramesPerSecond), falling.PendingFocus);
    }

    [Fact]
    public async Task Discard_RebuildsRowsFromTheSavedState()
    {
        await _viewModel.LoadAsync();
        Tab.MovementRows[0].FramesPerSecond.Text = "30";
        Size.Height.Text = "300";

        await _viewModel.DiscardAsync();

        Assert.Equal("10", Tab.MovementRows[0].FramesPerSecond.Text);
        Assert.Equal("100", Size.Height.Text);
        Assert.Equal((100, 100), _editor.CharacterSize);
    }

    private Task WaitForPreviewsAsync() =>
        ViewTestHelpers.WaitForPreviewsAsync(() => ViewTestHelpers.TabPreviews(_viewModel));

    private sealed class AcceptingDialogs : IUserDialogs
    {
        public bool Confirm(string title, string message) => true;

        public CloseDecision AskUnsavedChanges() => CloseDecision.Discard;
    }
}
