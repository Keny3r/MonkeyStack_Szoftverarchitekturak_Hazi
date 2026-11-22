namespace ConfigurationEngine.Tests.View;

using ConfigurationEngine.Model;
using ConfigurationEngine.View;
using ConfigurationEngine.View.Fakes;
using ConfigurationEngine.View.Tabs;
using MonkeyStack.Core.Events;

/// <summary>
/// A „Mintavételezés” fül (gui.md, "5. Mintavételezés") a minta konfigurációval: az akkumulátor mintavételezése ki van
/// kapcsolva, és a „Lemerülés” szabály használja.
/// </summary>
public sealed class PollingTabTests : IDisposable
{
    private readonly FakeConfigurationEditor _editor = new();
    private readonly ConfigurationWindowViewModel _viewModel;

    public PollingTabTests()
    {
        _viewModel = new ConfigurationWindowViewModel(_editor, new AcceptingDialogs());
    }

    private PollingTabViewModel Tab => _viewModel.PollingTab;

    public void Dispose() => _editor.Dispose();

    [Fact]
    public async Task Load_ShowsOneFixedRowPerMetricWithUnit()
    {
        await _viewModel.LoadAsync();

        Assert.Equal(Enum.GetValues<Metric>(), Tab.Rows.Select(r => r.Metric));
        Assert.Equal("CPU (%)", Row(Metric.Cpu).MetricLabel);
        Assert.Equal("Ablakok száma (db)", Row(Metric.WindowCount).MetricLabel);
        Assert.Equal("Inaktivitás (perc)", Row(Metric.Inactivity).MetricLabel);
        Assert.Equal("1000", Row(Metric.Cpu).IntervalMs.Text);
    }

    [Theory]
    [InlineData(1000, "= 1 s")]
    [InlineData(30000, "= 30 s")]
    [InlineData(60000, "= 1 perc")]
    public void Interval_HasAReadableForm(int milliseconds, string expected) =>
        Assert.Equal(expected, PollingRowViewModel.Readable(milliseconds));

    [Fact]
    public async Task RulesColumn_ListsRulesOfTheMetricAndWarnsWhenTheyCanNeverFire()
    {
        await _viewModel.LoadAsync();

        Assert.Equal("Kis tűz, Nagy tűz", Row(Metric.Cpu).RulesText);
        Assert.Equal("–", Row(Metric.Memory).RulesText);
        Assert.Equal("Lemerülés", Row(Metric.Battery).RulesText);
        Assert.True(Row(Metric.Battery).HasUnreachableRules);
        Assert.False(Row(Metric.Cpu).HasUnreachableRules);
    }

    [Fact]
    public async Task DisabledMetric_KeepsItsIntervalAndFlagsItsRulesInTheRuleList()
    {
        await _viewModel.LoadAsync();
        var cpu = Row(Metric.Cpu);
        cpu.IntervalMs.Text = "1500";

        cpu.IsEnabled = false;

        Assert.Equal(1500, cpu.Polling.IntervalMs);
        Assert.True(cpu.HasUnreachableRules);
        Assert.True(_viewModel.RulesTab.Rules.Single(r => r.DisplayName == "Kis tűz").IsMetricPollingDisabled);

        cpu.IsEnabled = true;
        Assert.Equal("1500", cpu.IntervalMs.Text);
    }

    [Theory]
    [InlineData("100")]
    [InlineData("70000")]
    [InlineData("gyakran")]
    public async Task InvalidInterval_IsAFieldError(string text)
    {
        await _viewModel.LoadAsync();
        var memory = Row(Metric.Memory);

        memory.IntervalMs.Text = text;

        Assert.True(memory.IntervalMs.HasErrors);
        Assert.True(memory.HasAnyError);
        Assert.Equal("Mintavételezés (1)", _viewModel.PollingTabHeader);
        Assert.False(_viewModel.CanSave);
    }

    [Fact]
    public async Task ResetDefaults_RestoresEveryRowInTheCopy()
    {
        await _viewModel.LoadAsync();
        Row(Metric.Cpu).IntervalMs.Text = "5000";
        Row(Metric.Disk).IntervalMs.Text = "abc";

        Tab.ResetDefaultsCommand.Execute(null);

        Assert.All(Tab.Rows, r =>
        {
            Assert.True(r.Polling.IsEnabled);
            Assert.Equal(ConfigurationDefaults.PollingIntervalMs(r.Metric), r.Polling.IntervalMs);
        });
        Assert.Equal("1000", Row(Metric.Cpu).IntervalMs.Text);
        Assert.Equal("30000", Row(Metric.Disk).IntervalMs.Text);
        Assert.False(Row(Metric.Battery).HasUnreachableRules);
        Assert.Equal(0, _viewModel.ErrorCount);
        Assert.True(_viewModel.IsDirty);
        Assert.Equal(0, _editor.SaveCount);
    }

    [Fact]
    public async Task GoToFirstError_FocusesTheIntervalField()
    {
        await _viewModel.LoadAsync();
        Row(Metric.Time).IntervalMs.Text = "1";

        _viewModel.GoToFirstErrorCommand.Execute(null);

        Assert.Equal(ConfigurationWindowViewModel.PollingTabIndex, _viewModel.SelectedTabIndex);
        Assert.Equal(nameof(MetricPolling.IntervalMs), Row(Metric.Time).PendingFocus);
    }

    private PollingRowViewModel Row(Metric metric) => Tab.Rows.Single(r => r.Metric == metric);

    private sealed class AcceptingDialogs : IUserDialogs
    {
        public bool Confirm(string title, string message) => true;

        public CloseDecision AskUnsavedChanges() => CloseDecision.Discard;
    }
}
