namespace ConfigurationEngine.Tests.View;

using System.IO;
using ConfigurationEngine.Model;
using ConfigurationEngine.Storage;
using ConfigurationEngine.Validation;
using ConfigurationEngine.View;
using ConfigurationEngine.View.Fakes;
using MonkeyStack.Core.Animation;

/// <summary>
/// A konfigurációs ablak fő nézetmodellje (gui.md, "Hibajelzés" és "Mentés") a memóriában tároló minta szerkesztővel.
/// </summary>
public sealed class ConfigurationWindowViewModelTests : IDisposable
{
    private readonly FakeConfigurationEditor _editor = new();
    private readonly TestDialogs _dialogs = new();
    private readonly ConfigurationWindowViewModel _viewModel;

    public ConfigurationWindowViewModelTests()
    {
        _viewModel = new ConfigurationWindowViewModel(_editor, _dialogs);
    }

    public void Dispose() => _editor.Dispose();

    [Fact]
    public async Task Load_ShowsDroppedItemsInWarningBar()
    {
        await _viewModel.LoadAsync();

        Assert.NotNull(_viewModel.Data);
        Assert.True(_viewModel.IsEditable);
        Assert.False(_viewModel.IsDirty);
        Assert.Equal(0, _viewModel.ErrorCount);
        Assert.True(_viewModel.HasWarning);
        var dropped = Assert.Single(_viewModel.DroppedItems);
        Assert.Equal("Régi szabály", dropped.Name);
        Assert.Contains("1 elem kimaradt", _viewModel.WarningText);
    }

    [Fact]
    public async Task Save_IsDisabledUntilSomethingChanges()
    {
        await _viewModel.LoadAsync();

        Assert.False(_viewModel.CanSave);
        Assert.False(_viewModel.SaveCommand.CanExecute(null));

        Rule("Pizsama").Name = "Alvás";
        _viewModel.NotifyEdited();

        Assert.True(_viewModel.IsDirty);
        Assert.True(_viewModel.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task ChangingBackToSavedValue_IsNotDirty()
    {
        await _viewModel.LoadAsync();

        Rule("Pizsama").Name = "Alvás";
        _viewModel.NotifyEdited();
        Rule("Alvás").Name = "Pizsama";
        _viewModel.NotifyEdited();

        Assert.False(_viewModel.IsDirty);
    }

    [Fact]
    public async Task FieldError_IsCountedOnItsTabAndDisablesSave()
    {
        await _viewModel.LoadAsync();

        Rule("Kis tűz").Threshold = 150;
        _viewModel.NotifyEdited();

        Assert.Equal(1, _viewModel.ErrorCount);
        Assert.Equal("Szabályok (1)", _viewModel.RulesTabHeader);
        Assert.Equal("Karakter", _viewModel.CharacterTabHeader);
        Assert.Equal("1 hiba", _viewModel.ErrorCountText);
        Assert.False(_viewModel.CanSave);
    }

    [Fact]
    public async Task InputError_DisablesSaveUntilCleared()
    {
        await _viewModel.LoadAsync();
        Rule("Pizsama").Name = "Alvás";
        _viewModel.NotifyEdited();

        _viewModel.SetInputError(ConfigurationItemKind.BehaviorRule, "3", nameof(BehaviorRule.Threshold), "Hibás idő.");

        Assert.Equal(1, _viewModel.ErrorCount);
        Assert.Equal("Szabályok (1)", _viewModel.RulesTabHeader);
        Assert.False(_viewModel.CanSave);

        _viewModel.SetInputError(ConfigurationItemKind.BehaviorRule, "3", nameof(BehaviorRule.Threshold), null);

        Assert.Equal(0, _viewModel.ErrorCount);
        Assert.True(_viewModel.CanSave);
    }

    [Fact]
    public async Task GoToFirstError_SelectsTabOfFirstErrorInTabOrder()
    {
        await _viewModel.LoadAsync();
        _viewModel.Data!.MetricPollings[0].IntervalMs = 1;
        Rule("Kis tűz").Threshold = 150;
        _viewModel.NotifyEdited();
        ValidationError? requested = null;
        _viewModel.ErrorNavigationRequested += (_, error) => requested = error;

        _viewModel.GoToFirstErrorCommand.Execute(null);

        Assert.Equal(ConfigurationWindowViewModel.RulesTabIndex, _viewModel.SelectedTabIndex);
        Assert.Equal(ConfigurationItemKind.BehaviorRule, requested?.Kind);
    }

    [Fact]
    public async Task DataReloaded_TellsWhyTheCopyWasReplaced()
    {
        var reasons = new List<DataReloadReason>();
        _viewModel.DataReloaded += (_, e) => reasons.Add(e.Reason);

        await _viewModel.LoadAsync();
        Rule("Pizsama").Name = "Alvás";
        _viewModel.NotifyEdited();
        await _viewModel.SaveAsync();
        await _viewModel.DiscardAsync();

        Assert.Equal([DataReloadReason.Opened, DataReloadReason.Saved, DataReloadReason.Discarded], reasons);
    }

    [Fact]
    public async Task Save_WhenReloadFails_ReportsSavedStateNotSaveFailure()
    {
        var editor = new FailingReloadEditor(_editor);
        var viewModel = new ConfigurationWindowViewModel(editor, _dialogs);
        await viewModel.LoadAsync();
        viewModel.Data!.BehaviorRules.Single(r => r.Name == "Pizsama").Name = "Alvás";
        viewModel.NotifyEdited();
        editor.FailLoad = true;

        var saved = await viewModel.SaveAsync();

        Assert.True(saved);
        Assert.Equal(1, _editor.SaveCount);
        Assert.False(viewModel.IsDirty);
        Assert.Empty(viewModel.DroppedItems);
        Assert.StartsWith("A mentés sikerült", viewModel.ErrorMessage);
    }

    [Fact]
    public async Task Previews_OfTheSameSource_ShareFrames()
    {
        var source = ConfigurationDefaults.CreateMovementSource(MonkeyStack.Core.Character.MovementState.Idle);

        var slow = await _editor.CreatePreviewAnimationAsync(source, 5);
        var fast = await _editor.CreatePreviewAnimationAsync(source, 30);

        Assert.Equal(2, _editor.PreviewCount);
        Assert.Same(slow.CurrentFrame, fast.CurrentFrame);

        _editor.ReleasePreviewAnimation(slow);
        Assert.Equal(1, _editor.PreviewCount);
    }

    [Fact]
    public async Task Save_WithDroppedItems_DoesNothingWhenNotConfirmed()
    {
        await _viewModel.LoadAsync();
        Rule("Pizsama").Name = "Alvás";
        _viewModel.NotifyEdited();
        _dialogs.ConfirmResult = false;

        var saved = await _viewModel.SaveAsync();

        Assert.False(saved);
        Assert.Equal(1, _dialogs.ConfirmCount);
        Assert.Equal(0, _editor.SaveCount);
        Assert.True(_viewModel.IsDirty);
    }

    [Fact]
    public async Task Save_StoresChangesAndReloadsSavedState()
    {
        await _viewModel.LoadAsync();
        Rule("Pizsama").Name = "Alvás";
        _viewModel.NotifyEdited();

        var saved = await _viewModel.SaveAsync();

        Assert.True(saved);
        Assert.Equal(1, _editor.SaveCount);
        Assert.False(_viewModel.IsDirty);
        Assert.Empty(_viewModel.DroppedItems);
        Assert.False(_viewModel.HasWarning);
        Assert.Equal("Mentve", _viewModel.StatusText);
        Assert.Contains(_viewModel.Data!.BehaviorRules, r => r.Name == "Alvás");
    }

    [Fact]
    public async Task Save_WithUnloadableMedia_ShowsErrorOnSourcesTabAndStoresNothing()
    {
        await _viewModel.LoadAsync();
        _viewModel.Data!.AnimationSources.Add(new AnimationSource
        {
            Id = -1,
            Name = "Hiányzó",
            SourceType = AnimationSourceType.Gif,
            Path = "nincs-ilyen.gif"
        });
        _viewModel.NotifyEdited();

        var saved = await _viewModel.SaveAsync();

        Assert.False(saved);
        Assert.Equal(0, _editor.SaveCount);
        Assert.StartsWith("Források (", _viewModel.SourcesTabHeader);
        Assert.True(_viewModel.IsDirty);
    }

    [Fact]
    public async Task Discard_RestoresSavedStateAndCharacterSize()
    {
        await _viewModel.LoadAsync();
        Rule("Pizsama").Name = "Alvás";
        _viewModel.Data!.Character.CharacterWidth = 200;
        _viewModel.ApplyCharacterSize(200, 100);
        _viewModel.NotifyEdited();
        Assert.Equal((200, 100), _editor.CharacterSize);

        await _viewModel.DiscardAsync();

        Assert.Equal(1, _dialogs.ConfirmCount);
        Assert.False(_viewModel.IsDirty);
        Assert.Contains(_viewModel.Data!.BehaviorRules, r => r.Name == "Pizsama");
        Assert.Equal((100, 100), _editor.CharacterSize);
    }

    [Fact]
    public async Task Close_WithUnsavedChanges_FollowsUserDecision()
    {
        await _viewModel.LoadAsync();
        Rule("Pizsama").Name = "Alvás";
        _viewModel.NotifyEdited();

        _dialogs.CloseDecision = CloseDecision.Cancel;
        Assert.False(await _viewModel.ConfirmCloseAsync());

        _dialogs.CloseDecision = CloseDecision.Discard;
        Assert.True(await _viewModel.ConfirmCloseAsync());
        Assert.Equal(0, _editor.SaveCount);
    }

    private BehaviorRule Rule(string name) => _viewModel.Data!.BehaviorRules.Single(r => r.Name == name);

    /// <summary>A minta szerkesztő, amelynek betöltése kérésre hibát dob (a mentés utáni újratöltés hibájához).</summary>
    private sealed class FailingReloadEditor(FakeConfigurationEditor inner) : IConfigurationEditor
    {
        public bool FailLoad { get; set; }

        public Task<ConfigurationLoadResult> LoadAsync(CancellationToken cancellationToken = default) =>
            FailLoad ? throw new IOException("Az adatbázis zárolt.") : inner.LoadAsync(cancellationToken);

        public ValidationResult Validate(ConfigurationData data) => inner.Validate(data);

        public Task<IReadOnlyList<ValidationError>> ValidateMediaAsync(
            AnimationSource source,
            CancellationToken cancellationToken = default) => inner.ValidateMediaAsync(source, cancellationToken);

        public Task SaveAsync(ConfigurationData data, CancellationToken cancellationToken = default) =>
            inner.SaveAsync(data, cancellationToken);

        public void SetCharacterSize(int width, int height) => inner.SetCharacterSize(width, height);

        public Task<IAnimation> CreatePreviewAnimationAsync(
            AnimationSource source,
            double framesPerSecond,
            CancellationToken cancellationToken = default) =>
            inner.CreatePreviewAnimationAsync(source, framesPerSecond, cancellationToken);

        public void ReleasePreviewAnimation(IAnimation preview) => inner.ReleasePreviewAnimation(preview);

        public void ReleasePreviewAnimations() => inner.ReleasePreviewAnimations();
    }

    private sealed class TestDialogs : IUserDialogs
    {
        public bool ConfirmResult { get; set; } = true;

        public int ConfirmCount { get; private set; }

        public CloseDecision CloseDecision { get; set; } = CloseDecision.Cancel;

        public bool Confirm(string title, string message)
        {
            ConfirmCount++;
            return ConfirmResult;
        }

        public CloseDecision AskUnsavedChanges() => CloseDecision;
    }
}
