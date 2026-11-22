namespace ConfigurationEngine.Tests.View;

using ConfigurationEngine.Model;
using ConfigurationEngine.View;
using ConfigurationEngine.View.Fakes;
using ConfigurationEngine.View.Tabs;
using MonkeyStack.Core.Events;

/// <summary>
/// A „Szabályok” fül (view.md, "3. Viselkedési szabályok (rétegek)") a minta konfigurációval:
/// Kis tűz (Id 1, Z 20), Nagy tűz (Id 2, Z 20), Pizsama (Z 10), Lemerülés (Z 5, kikapcsolt akkumulátor-mintavételezés).
/// </summary>
public sealed class RulesTabTests : IDisposable
{
    private readonly FakeConfigurationEditor _editor = new();
    private readonly CountingDialogs _dialogs = new();
    private readonly ConfigurationWindowViewModel _viewModel;

    public RulesTabTests()
    {
        _viewModel = new ConfigurationWindowViewModel(_editor, _dialogs);
    }

    private RulesTabViewModel Tab => _viewModel.RulesTab;

    public void Dispose() => _editor.Dispose();

    [Fact]
    public async Task List_IsOrderedByDescendingZIndexThenReverseDrawingOrder()
    {
        await _viewModel.LoadAsync();

        Assert.Equal(["Nagy tűz", "Kis tűz", "Pizsama", "Lemerülés"], Names());
        Assert.Same(Tab.Rules[0], Tab.SelectedRule);
    }

    [Fact]
    public async Task Rows_ShowShortConditionAndLayer()
    {
        await _viewModel.LoadAsync();

        Assert.Equal("CPU > 60 %", Row("Kis tűz").ConditionText);
        Assert.Equal("20:30–06:00", Row("Pizsama").ConditionText);
        Assert.Equal("Akkumulátor < 20 %", Row("Lemerülés").ConditionText);
        Assert.Equal("Z = 20", Row("Kis tűz").ZIndexText);
    }

    [Fact]
    public async Task RuleOfDisabledMetric_IsFlagged()
    {
        await _viewModel.LoadAsync();

        Assert.True(Row("Lemerülés").IsMetricPollingDisabled);
        Assert.False(Row("Kis tűz").IsMetricPollingDisabled);
    }

    [Fact]
    public async Task NewRule_IsTheTopLayerSelectedAndHasNoSource()
    {
        await _viewModel.LoadAsync();

        Tab.NewRuleCommand.Execute(null);

        var row = Assert.IsType<RuleRowViewModel>(Tab.SelectedRule);
        Assert.Same(Tab.Rules[0], row);
        Assert.Equal(21, row.Rule.ZIndex);
        Assert.True(row.Rule.Id < 0);
        Assert.Equal(0, row.Rule.AnimationSourceId);
        Assert.True(row.HasAnyError);
        Assert.True(_viewModel.IsDirty);
        Assert.Contains(row.Rule, _viewModel.Data!.BehaviorRules);
    }

    [Fact]
    public async Task NewRule_ZIndexIsCappedAt100()
    {
        await _viewModel.LoadAsync();
        Row("Kis tűz").ZIndex.Text = "100";

        Tab.NewRuleCommand.Execute(null);

        Assert.Equal(100, Tab.SelectedRule!.Rule.ZIndex);
    }

    [Fact]
    public async Task Delete_RemovesSelectedRuleWithoutConfirmation()
    {
        await _viewModel.LoadAsync();
        Tab.SelectedRule = Row("Pizsama");

        Tab.DeleteCommand.Execute(null);

        Assert.Equal(["Nagy tűz", "Kis tűz", "Lemerülés"], Names());
        Assert.DoesNotContain(_viewModel.Data!.BehaviorRules, r => r.Name == "Pizsama");
        Assert.Same(Row("Lemerülés"), Tab.SelectedRule);
        Assert.Equal(0, _dialogs.ConfirmCount);
        Assert.True(_viewModel.IsDirty);
    }

    [Fact]
    public async Task ZIndexChange_ResortsOnlyWhenTheFieldIsLeft()
    {
        await _viewModel.LoadAsync();

        Row("Pizsama").ZIndex.Text = "30";
        Assert.Equal(["Nagy tűz", "Kis tűz", "Pizsama", "Lemerülés"], Names());

        Tab.Resort();
        Assert.Equal(["Pizsama", "Nagy tűz", "Kis tűz", "Lemerülés"], Names());
    }

    [Fact]
    public async Task TimeThresholds_AreEditedAsHoursAndMinutes()
    {
        await _viewModel.LoadAsync();
        var pyjama = Row("Pizsama");
        Assert.Equal("20:30", pyjama.Threshold.Text);
        Assert.Equal("06:00", pyjama.ThresholdUpper.Text);
        Assert.True(pyjama.CrossesMidnight);

        pyjama.Threshold.Text = "21:15";
        Assert.Equal(1275, pyjama.Rule.Threshold);

        pyjama.Threshold.Text = "25:00";
        Assert.True(pyjama.Threshold.HasInputError);
        Assert.Equal(1275, pyjama.Rule.Threshold);
    }

    [Fact]
    public async Task MetricChange_KeepsThresholdsAndChangesTheirForm()
    {
        await _viewModel.LoadAsync();
        var small = Row("Kis tűz");

        small.Metric = Metric.Time;

        Assert.Equal(60, small.Rule.Threshold);
        Assert.Equal("01:00", small.Threshold.Text);
        Assert.Equal(string.Empty, small.UnitText);

        small.Metric = Metric.WindowCount;
        Assert.Equal("60", small.Threshold.Text);
        Assert.Equal("db", small.UnitText);
    }

    [Fact]
    public async Task OperatorChange_ClearsOrRequiresTheUpperBound()
    {
        await _viewModel.LoadAsync();
        var pyjama = Row("Pizsama");

        pyjama.Operator = ComparisonOperator.Gte;
        Assert.Null(pyjama.Rule.ThresholdUpper);
        Assert.False(pyjama.IsBetween);

        pyjama.Operator = ComparisonOperator.Between;
        Assert.True(pyjama.IsBetween);
        Assert.True(pyjama.ThresholdUpper.HasErrors);
        Assert.False(_viewModel.CanSave);
    }

    [Fact]
    public async Task BlankGroup_MeansNoGroupAndDisablesPriority()
    {
        await _viewModel.LoadAsync();
        var small = Row("Kis tűz");
        Assert.True(small.HasGroup);

        small.ExclusiveGroup = string.Empty;
        Assert.Null(small.Rule.ExclusiveGroup);
        Assert.False(small.HasGroup);

        small.ExclusiveGroup = "   ";
        Assert.False(small.HasGroup);
    }

    [Fact]
    public async Task GroupNames_OfferGroupsOfOtherRules()
    {
        await _viewModel.LoadAsync();

        Tab.SelectedRule = Row("Pizsama");

        Assert.Equal(["fire"], Tab.GroupNames);
    }

    [Fact]
    public async Task EnablingFromTheList_GoesIntoTheCopy()
    {
        await _viewModel.LoadAsync();

        Row("Kis tűz").IsEnabled = false;

        Assert.False(_viewModel.Data!.BehaviorRules.Single(r => r.Name == "Kis tűz").IsEnabled);
        Assert.True(_viewModel.IsDirty);
    }

    [Fact]
    public async Task GoToFirstError_SelectsTheRuleAndFocusesTheField()
    {
        await _viewModel.LoadAsync();
        Row("Lemerülés").Threshold.Text = "150";

        _viewModel.GoToFirstErrorCommand.Execute(null);

        Assert.Equal(ConfigurationWindowViewModel.RulesTabIndex, _viewModel.SelectedTabIndex);
        Assert.Same(Row("Lemerülés"), Tab.SelectedRule);
        Assert.Equal(nameof(BehaviorRule.Threshold), Row("Lemerülés").PendingFocus);
    }

    [Fact]
    public async Task SavedNewRule_GetsARealIdAndStaysOnTop()
    {
        await _viewModel.LoadAsync();
        Tab.NewRuleCommand.Execute(null);
        var row = Tab.SelectedRule!;
        row.Name.Text = "Éjszakai bagoly";
        row.AnimationSourceId = 1;

        Assert.True(await _viewModel.SaveAsync());

        Assert.Equal("Éjszakai bagoly", Tab.Rules[0].DisplayName);
        Assert.True(Tab.Rules[0].Rule.Id > 0);
        Assert.False(_viewModel.IsDirty);
    }

    private List<string> Names() => Tab.Rules.Select(r => r.DisplayName).ToList();

    private RuleRowViewModel Row(string name) => Tab.Rules.Single(r => r.DisplayName == name);

    private sealed class CountingDialogs : IUserDialogs
    {
        public int ConfirmCount { get; private set; }

        public bool Confirm(string title, string message)
        {
            ConfirmCount++;
            return true;
        }

        public CloseDecision AskUnsavedChanges() => CloseDecision.Discard;
    }
}
