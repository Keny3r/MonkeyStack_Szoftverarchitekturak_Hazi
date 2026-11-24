namespace ConfigurationEngine.Tests;

using ConfigurationEngine.Model;
using ConfigurationEngine.Validation;
using MonkeyStack.Core.Character;
using MonkeyStack.Core.Events;

public class ConfigurationValidatorTests
{
    private readonly ConfigurationValidator _validator = new();

    [Fact]
    public void ValidConfiguration_HasNoErrors()
    {
        var result = _validator.Validate(TestData.ValidConfiguration());

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }

    [Fact]
    public void ShippedDefaults_HaveNoErrors()
    {
        var data = new ConfigurationData
        {
            AnimationSources = [.. ConfigurationDefaults.CreateMovementSources(), .. ConfigurationDefaults.CreateOverlaySources()],
            MovementAnimations = ConfigurationDefaults.CreateMovementAnimations(),
            BehaviorRules = ConfigurationDefaults.CreateDefaultRules(),
            Character = ConfigurationDefaults.CreateCharacterSettings(),
            MetricPollings = ConfigurationDefaults.CreateMetricPollings()
        };

        var result = _validator.Validate(data);

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }

    [Theory]
    [InlineData(Metric.Cpu, 150)]
    [InlineData(Metric.Cpu, -1)]
    [InlineData(Metric.Time, 1440)]
    [InlineData(Metric.Inactivity, 2000)]
    [InlineData(Metric.WindowCount, 1001)]
    public void ThresholdOutsideMetricRange_IsRejected(Metric metric, int threshold)
    {
        var data = TestData.ValidConfiguration();
        data.BehaviorRules.Add(TestData.Rule(10, "Hibás", metric, ComparisonOperator.Gt, threshold));

        AssertSingleError(data, ConfigurationItemKind.BehaviorRule, "10", nameof(BehaviorRule.Threshold));
    }

    [Fact]
    public void BetweenAcrossMidnight_IsAllowedForTime()
    {
        var data = TestData.ValidConfiguration();
        data.BehaviorRules.Add(TestData.Rule(10, "Éjszaka", Metric.Time, ComparisonOperator.Between, 1320, upper: 300));

        Assert.True(_validator.Validate(data).IsValid);
    }

    [Fact]
    public void BetweenWithUpperBelowLower_IsRejectedForOtherMetrics()
    {
        var data = TestData.ValidConfiguration();
        data.BehaviorRules.Add(TestData.Rule(10, "Fordított", Metric.Memory, ComparisonOperator.Between, 95, upper: 90));

        AssertSingleError(data, ConfigurationItemKind.BehaviorRule, "10", nameof(BehaviorRule.ThresholdUpper));
    }

    [Fact]
    public void BetweenWithoutUpper_IsRejected()
    {
        var data = TestData.ValidConfiguration();
        data.BehaviorRules.Add(TestData.Rule(10, "Félkész", Metric.Disk, ComparisonOperator.Between, 90));

        AssertSingleError(data, ConfigurationItemKind.BehaviorRule, "10", nameof(BehaviorRule.ThresholdUpper));
    }

    [Fact]
    public void UpperWithoutBetween_IsRejected()
    {
        var data = TestData.ValidConfiguration();
        data.BehaviorRules.Add(TestData.Rule(10, "Fölösleges", Metric.Battery, ComparisonOperator.Lt, 20, upper: 50));

        AssertSingleError(data, ConfigurationItemKind.BehaviorRule, "10", nameof(BehaviorRule.ThresholdUpper));
    }

    [Fact]
    public void UnknownEnumValues_AreRejected()
    {
        var data = TestData.ValidConfiguration();
        data.BehaviorRules[0].Metric = (Metric)(-1);
        data.BehaviorRules[1].Operator = (ComparisonOperator)(-1);
        data.AnimationSources[0].SourceType = (AnimationSourceType)(-1);

        var errors = _validator.Validate(data).Errors;

        Assert.Contains(errors, e => e.ItemKey == "1" && e.PropertyName == nameof(BehaviorRule.Metric));
        Assert.Contains(errors, e => e.ItemKey == "2" && e.PropertyName == nameof(BehaviorRule.Operator));
        Assert.Contains(errors, e => e.Kind == ConfigurationItemKind.AnimationSource && e.PropertyName == nameof(AnimationSource.SourceType));
    }

    [Fact]
    public void UnknownSourceType_ReportsOnlyTheType()
    {
        var data = TestData.ValidConfiguration();
        data.AnimationSources[1].SourceType = (AnimationSourceType)(-1); // a "Tűz" sprite sheet, kitöltött ráccsal

        AssertSingleError(data, ConfigurationItemKind.AnimationSource, "2", nameof(AnimationSource.SourceType));
    }

    [Fact]
    public void UnknownOperator_ReportsOnlyTheOperator()
    {
        var data = TestData.ValidConfiguration();
        data.BehaviorRules[2].Operator = (ComparisonOperator)(-1); // a "Pizsama" szabály, felső határral

        AssertSingleError(data, ConfigurationItemKind.BehaviorRule, "3", nameof(BehaviorRule.Operator));
    }

    [Fact]
    public void DuplicateRuleId_IsKeyedByIndex()
    {
        var data = TestData.ValidConfiguration();
        data.BehaviorRules.Add(TestData.Rule(1, "Másolat", Metric.Cpu, ComparisonOperator.Gt, 50));

        AssertSingleError(data, ConfigurationItemKind.BehaviorRule, "#3", nameof(BehaviorRule.Id));
    }

    [Fact]
    public void DuplicateSourceId_IsKeyedByIndex()
    {
        var data = TestData.ValidConfiguration();
        data.AnimationSources.Add(new AnimationSource { Id = 1, Name = "Másolat", SourceType = AnimationSourceType.Gif, Path = "copy.gif" });

        AssertSingleError(data, ConfigurationItemKind.AnimationSource, "#3", nameof(AnimationSource.Id));
    }

    [Fact]
    public void DuplicateSourceName_IsRejectedIgnoringCase()
    {
        var data = TestData.ValidConfiguration();
        data.AnimationSources.Add(new AnimationSource { Id = 4, Name = " TŰZ ", SourceType = AnimationSourceType.Gif, Path = "x.gif" });

        AssertSingleError(data, ConfigurationItemKind.AnimationSource, "4", nameof(AnimationSource.Name));
    }

    [Fact]
    public void MissingSourceReference_IsRejected()
    {
        var data = TestData.ValidConfiguration();
        data.BehaviorRules[0].AnimationSourceId = 99;

        AssertSingleError(data, ConfigurationItemKind.BehaviorRule, "1", nameof(BehaviorRule.AnimationSourceId));
    }

    [Fact]
    public void NewItemsWithTemporaryIds_CanReferenceEachOther()
    {
        var data = TestData.ValidConfiguration();
        data.AnimationSources.Add(new AnimationSource { Id = -1, Name = "Új", SourceType = AnimationSourceType.Gif, Path = "new.gif" });
        data.BehaviorRules.Add(TestData.Rule(0, "Új szabály", Metric.Cpu, ComparisonOperator.Gte, 99, sourceId: -1));

        Assert.True(_validator.Validate(data).IsValid);
    }

    [Fact]
    public void NewItemWithoutId_IsKeyedByIndex()
    {
        var data = TestData.ValidConfiguration();
        data.BehaviorRules.Add(TestData.Rule(0, "", Metric.Cpu, ComparisonOperator.Gt, 50));

        AssertSingleError(data, ConfigurationItemKind.BehaviorRule, "#3", nameof(BehaviorRule.Name));
    }

    [Fact]
    public void SpriteSheetGrid_IsRequiredAndConsistent()
    {
        var data = TestData.ValidConfiguration();
        data.AnimationSources[1].FrameCount = 9; // 4 × 2 = 8 a maximum

        AssertSingleError(data, ConfigurationItemKind.AnimationSource, "2", nameof(AnimationSource.FrameCount));

        data.AnimationSources[1].FrameCount = 7;
        data.AnimationSources[1].Columns = null;

        AssertSingleError(data, ConfigurationItemKind.AnimationSource, "2", nameof(AnimationSource.Columns));
    }

    [Fact]
    public void GridFields_AreRejectedForNonSpriteSheets()
    {
        var data = TestData.ValidConfiguration();
        data.AnimationSources[0].Columns = 3;

        AssertSingleError(data, ConfigurationItemKind.AnimationSource, "1", nameof(AnimationSource.Columns));
    }

    [Fact]
    public void DuplicateMovementState_IsRejected()
    {
        var data = TestData.ValidConfiguration();
        data.MovementAnimations.Add(new MovementAnimation { MovementState = MovementState.Walking, AnimationSourceId = 1, FramesPerSecond = 8 });

        AssertSingleError(data, ConfigurationItemKind.MovementAnimation, nameof(MovementState.Walking), nameof(MovementAnimation.MovementState));
    }

    [Fact]
    public void MissingMovementState_IsRejected()
    {
        var data = TestData.ValidConfiguration();
        data.MovementAnimations.RemoveAll(m => m.MovementState == MovementState.Falling);

        AssertSingleError(data, ConfigurationItemKind.MovementAnimation, nameof(MovementState.Falling), nameof(MovementAnimation.AnimationSourceId));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(61.0)]
    [InlineData(double.NaN)]
    public void FramesPerSecondOutsideRange_IsRejected(double fps)
    {
        var data = TestData.ValidConfiguration();
        data.MovementAnimations.Single(m => m.MovementState == MovementState.Walking).FramesPerSecond = fps;

        AssertSingleError(data, ConfigurationItemKind.MovementAnimation, nameof(MovementState.Walking), nameof(MovementAnimation.FramesPerSecond));
    }

    [Fact]
    public void WhitespaceExclusiveGroup_MeansNoGroup()
    {
        var data = TestData.ValidConfiguration();
        data.BehaviorRules[0].ExclusiveGroup = "   ";

        Assert.True(_validator.Validate(data).IsValid);
    }

    [Fact]
    public void TooLongExclusiveGroup_IsRejected()
    {
        var data = TestData.ValidConfiguration();
        data.BehaviorRules[0].ExclusiveGroup = new string('x', ConfigurationLimits.ExclusiveGroupMaxLength + 1);

        AssertSingleError(data, ConfigurationItemKind.BehaviorRule, "1", nameof(BehaviorRule.ExclusiveGroup));
    }

    [Fact]
    public void CharacterSizeOutsideRange_IsRejected()
    {
        var data = TestData.ValidConfiguration();
        data.Character.CharacterWidth = 8;

        AssertSingleError(data, ConfigurationItemKind.CharacterSettings, "1", nameof(CharacterSettings.CharacterWidth));
    }

    [Fact]
    public void PollingIntervalOutsideRange_IsRejected()
    {
        var data = TestData.ValidConfiguration();
        data.MetricPollings[0].IntervalMs = 100;

        AssertSingleError(data, ConfigurationItemKind.MetricPolling, nameof(Metric.Cpu), nameof(MetricPolling.IntervalMs));
    }

    private void AssertSingleError(ConfigurationData data, ConfigurationItemKind kind, string itemKey, string propertyName)
    {
        var error = Assert.Single(_validator.Validate(data).Errors);
        Assert.Equal(kind, error.Kind);
        Assert.Equal(itemKey, error.ItemKey);
        Assert.Equal(propertyName, error.PropertyName);
        Assert.False(string.IsNullOrWhiteSpace(error.Message));
    }
}
