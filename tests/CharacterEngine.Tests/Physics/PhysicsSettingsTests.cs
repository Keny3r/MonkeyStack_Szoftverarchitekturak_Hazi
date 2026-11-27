namespace CharacterEngine.Tests.Physics;

using CharacterEngine.Physics;
using ConfigurationEngine.Model;

public class PhysicsSettingsTests
{
    [Fact]
    public void Default_MatchesConfigurationSchemaDefaults()
    {
        var schemaDefaults = new CharacterSettings();

        Assert.Equal(schemaDefaults.GravityPxPerSec2, PhysicsSettings.Default.GravityPxPerSec2);
        Assert.Equal(schemaDefaults.WalkSpeedPxPerSec, PhysicsSettings.Default.WalkSpeedPxPerSec);
    }

    [Fact]
    public void Default_HasPositiveLimits()
    {
        var settings = PhysicsSettings.Default;

        Assert.True(settings.GravityPxPerSec2 > 0);
        Assert.True(settings.WalkSpeedPxPerSec > 0);
        Assert.True(settings.MaxFallSpeedPxPerSec > 0);
        Assert.True(settings.MaxDeltaTime > TimeSpan.Zero);
        Assert.True(settings.SupportTolerancePx > 0);
    }

    [Fact]
    public void With_OverridesOnlyTheGivenValue()
    {
        var custom = PhysicsSettings.Default with { GravityPxPerSec2 = 1000.0 };

        Assert.Equal(1000.0, custom.GravityPxPerSec2);
        Assert.Equal(PhysicsSettings.Default.WalkSpeedPxPerSec, custom.WalkSpeedPxPerSec);
    }
}
