namespace ConfigurationEngine.Tests.Rules;

using ConfigurationEngine.Model;
using ConfigurationEngine.Rules;
using MonkeyStack.Core.Events;
using static ConfigurationEngine.Tests.TestData;

public sealed class RuleEvaluatorTests
{
    private static readonly IReadOnlySet<Metric> NoneDisabled = new HashSet<Metric>();

    [Fact]
    public void SchemaExample_BurningInPyjamas()
    {
        // schema.md, "Kiértékelés": CPU = 85%, 22:00 → Pizsama (ZIndex 10), majd Nagy tűz (ZIndex 20).
        var rules = ValidConfiguration().BehaviorRules;

        var active = Select(rules, Registry(cpu: 85, hour: 22));

        Assert.Equal(["Pizsama", "Nagy tűz"], active);
    }

    [Fact]
    public void ExclusiveGroup_KeepsOnlyHighestPriority()
    {
        var rules = ValidConfiguration().BehaviorRules;

        Assert.Equal(["Kis tűz"], Select(rules, Registry(cpu: 70, hour: 12)));
        Assert.Empty(Select(rules, Registry(cpu: 50, hour: 12)));
    }

    [Fact]
    public void ExclusiveGroup_EqualPriority_SmallerIdWins()
    {
        BehaviorRule[] rules =
        [
            Rule(5, "Később felvett", Metric.Cpu, ComparisonOperator.Gt, 10, group: "g", priority: 3),
            Rule(4, "Korábban felvett", Metric.Cpu, ComparisonOperator.Gt, 20, group: "g", priority: 3)
        ];

        Assert.Equal(["Korábban felvett"], Select(rules, Registry(cpu: 50)));
    }

    [Fact]
    public void EqualZIndex_IsOrderedById()
    {
        BehaviorRule[] rules =
        [
            Rule(9, "B", Metric.Cpu, ComparisonOperator.Gt, 10, zIndex: 5),
            Rule(3, "A", Metric.Memory, ComparisonOperator.Gt, 10, zIndex: 5),
            Rule(7, "Alul", Metric.Cpu, ComparisonOperator.Gt, 10, zIndex: 1)
        ];

        Assert.Equal(["Alul", "A", "B"], Select(rules, Registry(cpu: 50, memory: 50)));
    }

    [Fact]
    public void MissingMetricData_IsNotSatisfied()
    {
        BehaviorRule[] rules = [Rule(1, "Kevés akku", Metric.Battery, ComparisonOperator.Lt, 20)];

        Assert.Empty(Select(rules, new OsEventDataRegistry()));
    }

    [Fact]
    public void DisabledRuleOrDisabledMetric_IsNotSatisfied()
    {
        var disabled = Rule(1, "Kikapcsolt", Metric.Cpu, ComparisonOperator.Gt, 10);
        disabled.IsEnabled = false;
        BehaviorRule[] rules = [disabled, Rule(2, "Kikapcsolt metrika", Metric.Memory, ComparisonOperator.Gt, 10)];

        var active = RuleEvaluator.SelectActive(rules, r => r, Registry(cpu: 50, memory: 50), new HashSet<Metric> { Metric.Memory });

        Assert.Empty(active);
    }

    [Theory]
    [InlineData(ComparisonOperator.Gt, 60, false)]
    [InlineData(ComparisonOperator.Gt, 61, true)]
    [InlineData(ComparisonOperator.Gte, 60, true)]
    [InlineData(ComparisonOperator.Gte, 59, false)]
    [InlineData(ComparisonOperator.Lt, 60, false)]
    [InlineData(ComparisonOperator.Lt, 59, true)]
    [InlineData(ComparisonOperator.Lte, 60, true)]
    [InlineData(ComparisonOperator.Lte, 61, false)]
    public void Operators_RespectBoundary(ComparisonOperator op, int value, bool expected)
    {
        Assert.Equal(expected, RuleEvaluator.IsSatisfied(Rule(1, "r", Metric.Cpu, op, 60), value));
    }

    [Theory]
    [InlineData(40, false)]
    [InlineData(50, true)]
    [InlineData(70, true)]
    [InlineData(71, false)]
    public void Between_IsClosedInterval(int value, bool expected)
    {
        var rule = Rule(1, "r", Metric.Cpu, ComparisonOperator.Between, 50, upper: 70);

        Assert.Equal(expected, RuleEvaluator.IsSatisfied(rule, value));
    }

    [Theory]
    [InlineData(20, 29, false)]
    [InlineData(20, 30, true)]
    [InlineData(23, 59, true)]
    [InlineData(0, 0, true)]
    [InlineData(6, 0, true)]
    [InlineData(6, 1, false)]
    [InlineData(12, 0, false)]
    public void TimeBetween_CanSpanMidnight(int hour, int minute, bool expected)
    {
        // 20:30–06:00 (schema.md, "Metrikák és értéktartományok").
        BehaviorRule[] rules = [Rule(1, "Pizsama", Metric.Time, ComparisonOperator.Between, 1230, upper: 360)];

        Assert.Equal(expected, Select(rules, Registry(hour: hour, minute: minute)).Count == 1);
    }

    [Fact]
    public void EveryMetric_IsReadFromItsRegistryField()
    {
        var registry = new OsEventDataRegistry
        {
            Cpu = new CpuEventData { UsagePercent = 1 },
            Memory = new MemoryEventData { UsagePercent = 2 },
            Disk = new DiskEventData { UsagePercent = 3 },
            Battery = new BatteryEventData { ChargePercent = 4 },
            WindowCount = new WindowCountEventData { WindowCount = 5 },
            Time = new TimeEventData { Hour = 1, Minute = 6 },
            Inactivity = new InactivityEventData { InactivityMinutes = 7 }
        };

        var values = Enum.GetValues<Metric>().Select(m => RuleEvaluator.TryReadMetric(registry, m, out int v) ? v : -1);

        Assert.Equal([1, 2, 3, 4, 5, 66, 7], values);
    }

    private static List<string> Select(IEnumerable<BehaviorRule> rules, OsEventDataRegistry registry) =>
        RuleEvaluator.SelectActive(rules, r => r, registry, NoneDisabled).ConvertAll(r => r.Name);

    private static OsEventDataRegistry Registry(int? cpu = null, int? memory = null, int? hour = null, int minute = 0) => new()
    {
        Cpu = cpu is { } c ? new CpuEventData { UsagePercent = c } : null,
        Memory = memory is { } m ? new MemoryEventData { UsagePercent = m } : null,
        Time = hour is { } h ? new TimeEventData { Hour = h, Minute = minute } : null
    };
}
