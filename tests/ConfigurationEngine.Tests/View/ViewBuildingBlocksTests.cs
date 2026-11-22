namespace ConfigurationEngine.Tests.View;

using System.Globalization;
using ConfigurationEngine.Model;
using ConfigurationEngine.Validation;
using ConfigurationEngine.View;
using ConfigurationEngine.View.Fakes;

/// <summary>
/// A fülek közös építőelemei (gui.md, "Mezőnkénti hibajelzés" és "Élő előnézet"): mezőnkénti hibák, beviteli hibák,
/// ugrás a hibás mezőre, a forrásválasztó lista és az előnézetek élettartama. Egy egyszerű, csak a teszthez készült
/// szabályfülön keresztül.
/// </summary>
public sealed class ViewBuildingBlocksTests : IDisposable
{
    private readonly FakeConfigurationEditor _editor = new();
    private readonly ConfigurationWindowViewModel _viewModel;
    private readonly TestRulesTab _tab;

    public ViewBuildingBlocksTests()
    {
        _viewModel = new ConfigurationWindowViewModel(_editor, new AcceptingDialogs());
        _tab = new TestRulesTab(_viewModel);
    }

    public void Dispose() => _editor.Dispose();

    [Fact]
    public async Task InvalidText_IsAnInputErrorAndDoesNotReachTheCopy()
    {
        await _viewModel.LoadAsync();
        var row = _tab.Row("Kis tűz");

        row.Threshold.Text = "sok";

        Assert.Equal(60, row.Rule.Threshold);
        Assert.True(row.Threshold.HasInputError);
        Assert.Equal("Egész számot adj meg.", row.Threshold.ErrorText);
        Assert.True(row.HasAnyError);
        Assert.Equal(1, _viewModel.ErrorCount);
        Assert.False(_viewModel.IsDirty);

        row.Threshold.Text = "70";

        Assert.Equal(70, row.Rule.Threshold);
        Assert.False(row.Threshold.HasInputError);
        Assert.Equal(string.Empty, row.Threshold.ErrorText);
        Assert.Equal(0, _viewModel.ErrorCount);
        Assert.True(_viewModel.IsDirty);
    }

    [Fact]
    public async Task ValidatorError_IsShownAtItsField()
    {
        await _viewModel.LoadAsync();
        var row = _tab.Row("Kis tűz");

        row.Threshold.Text = "150";

        Assert.Equal(150, row.Rule.Threshold);
        Assert.False(row.Threshold.HasInputError);
        Assert.True(row.Threshold.HasErrors);
        Assert.NotEmpty(row.Threshold.ErrorText);
        Assert.True(row.HasAnyError);
        Assert.False(_tab.Row("Nagy tűz").HasAnyError);
    }

    [Fact]
    public async Task ErrorOfNonTextField_IsAvailableByPropertyName()
    {
        await _viewModel.LoadAsync();
        var row = _tab.Row("Kis tűz");

        row.Rule.AnimationSourceId = 99;
        _viewModel.NotifyEdited();

        Assert.NotEmpty(row.GetErrors(nameof(BehaviorRule.AnimationSourceId)).Cast<string>());
        Assert.NotEmpty(row.FieldErrors[nameof(BehaviorRule.AnimationSourceId)]);
        Assert.Equal(string.Empty, row.FieldErrors[nameof(BehaviorRule.Name)]);
    }

    [Fact]
    public async Task GoToFirstError_SelectsRowAndRequestsFocusOnField()
    {
        await _viewModel.LoadAsync();
        var row = _tab.Row("Nagy tűz");
        row.Threshold.Text = "150";

        _viewModel.GoToFirstErrorCommand.Execute(null);

        Assert.Equal(ConfigurationWindowViewModel.RulesTab, _viewModel.SelectedTabIndex);
        Assert.Same(row, _tab.Selected);
        Assert.Equal(nameof(BehaviorRule.Threshold), row.PendingFocus);
    }

    [Fact]
    public async Task SourceOptions_FollowTheEditedCopyInPlace()
    {
        await _viewModel.LoadAsync();
        Assert.Equal(_viewModel.Data!.AnimationSources.Select(s => s.Id), _viewModel.SourceOptions.Select(o => o.Id));
        var first = _viewModel.SourceOptions[0];

        _viewModel.Data.AnimationSources.Add(new AnimationSource
        {
            Id = -1,
            Name = "Új",
            SourceType = AnimationSourceType.ImageFolder,
            Path = "movement/idle"
        });
        _viewModel.Data.AnimationSources[0].Name = "Átnevezett";
        _viewModel.NotifyEdited();

        Assert.Contains(_viewModel.SourceOptions, o => o.Id == -1 && o.Name == "Új");
        Assert.Same(first, _viewModel.SourceOptions[0]);
        Assert.Equal("Átnevezett", first.Name);
    }

    [Fact]
    public async Task SpeedChange_ReplacesPreviewAndReleasesTheOldOne()
    {
        await _viewModel.LoadAsync();
        await WaitForPreviewsAsync();
        var row = _tab.Row("Kis tűz");
        var before = row.Preview.Animation;

        row.Rule.FramesPerSecond = 25;
        _viewModel.NotifyEdited();
        await WaitForPreviewsAsync();

        Assert.NotSame(before, row.Preview.Animation);
        Assert.Equal(25, row.Preview.Animation!.FramesPerSecond);
        Assert.Equal(_tab.Rows.Count, _editor.PreviewCount);
    }

    [Fact]
    public async Task RemovedRow_ReleasesOnlyItsPreview()
    {
        await _viewModel.LoadAsync();
        await WaitForPreviewsAsync();
        var row = _tab.Row("Kis tűz");

        row.Preview.Release();

        Assert.Null(row.Preview.Animation);
        Assert.Equal(_tab.Rows.Count - 1, _editor.PreviewCount);
    }

    [Fact]
    public async Task Save_KeepsPreviews()
    {
        await _viewModel.LoadAsync();
        await WaitForPreviewsAsync();
        var before = _tab.Row("Pizsama").Preview.Animation;
        _tab.Row("Pizsama").Rule.Name = "Alvás";
        _viewModel.NotifyEdited();

        Assert.True(await _viewModel.SaveAsync());

        Assert.Same(before, _tab.Row("Alvás").Preview.Animation);
        Assert.Equal(_tab.Rows.Count, _editor.PreviewCount);
    }

    [Fact]
    public async Task Discard_ReleasesAllPreviewsAndRequestsNewOnes()
    {
        await _viewModel.LoadAsync();
        await WaitForPreviewsAsync();
        var before = _tab.Row("Pizsama").Preview.Animation;

        await _viewModel.DiscardAsync();
        await WaitForPreviewsAsync();

        Assert.NotSame(before, _tab.Row("Pizsama").Preview.Animation);
        Assert.Equal(_tab.Rows.Count, _editor.PreviewCount);
    }

    [Fact]
    public async Task UnloadableSource_ShowsErrorInsteadOfPreview()
    {
        await _viewModel.LoadAsync();
        var preview = _viewModel.Previews.Create();

        preview.Show(
            new AnimationSource { Id = -1, Name = "Hiányzó", SourceType = AnimationSourceType.Gif, Path = "nincs.gif" },
            10);
        await WaitUntilAsync(() => !preview.IsLoading);

        Assert.Null(preview.Animation);
        Assert.True(preview.HasError);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(25);
        }

        Assert.True(condition());
    }

    private Task WaitForPreviewsAsync() =>
        WaitUntilAsync(() => _tab.Rows.All(r => r.Preview is { IsLoading: false, Animation: not null }));

    private sealed class TestRulesTab(ConfigurationWindowViewModel owner) : TabViewModel(owner)
    {
        public List<TestRuleRow> Rows { get; private set; } = [];

        public EditableItemViewModel? Selected { get; private set; }

        public override int TabIndex => ConfigurationWindowViewModel.RulesTab;

        protected override IEnumerable<EditableItemViewModel> Items => Rows;

        public TestRuleRow Row(string name) => Rows.Single(r => r.Rule.Name == name);

        protected override void Rebuild(DataReloadReason reason) =>
            Rows = Owner.Data!.BehaviorRules.Select(r => new TestRuleRow(Owner, r)).ToList();

        protected override void RefreshPreviews()
        {
            foreach (var row in Rows)
            {
                row.Preview.Show(Owner.FindSource(row.Rule.AnimationSourceId), row.Rule.FramesPerSecond);
            }
        }

        protected override void Select(EditableItemViewModel item) => Selected = item;
    }

    private sealed class TestRuleRow : EditableItemViewModel
    {
        public TestRuleRow(ConfigurationWindowViewModel owner, BehaviorRule rule)
            : base(owner)
        {
            Rule = rule;
            Threshold = IntField(nameof(BehaviorRule.Threshold), () => rule.Threshold, v => rule.Threshold = v);
            Preview = owner.Previews.Create();
        }

        public BehaviorRule Rule { get; }

        public InputField Threshold { get; }

        public PreviewViewModel Preview { get; }

        public override ConfigurationItemKind Kind => ConfigurationItemKind.BehaviorRule;

        public override string ItemKey => Rule.Id.ToString(CultureInfo.InvariantCulture);
    }

    private sealed class AcceptingDialogs : IUserDialogs
    {
        public bool Confirm(string title, string message) => true;

        public CloseDecision AskUnsavedChanges() => CloseDecision.Discard;
    }
}
