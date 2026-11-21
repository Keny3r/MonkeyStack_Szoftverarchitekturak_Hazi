namespace OsEventEngine.Tests;

using MonkeyStack.Core.Character;

public class PlatformContractTests
{
    [Fact]
    public void EmptySnapshotHasNoPlatformsAndVersionZero()
    {
        var empty = PlatformSnapshot.Empty;

        Assert.Empty(empty.Platforms);
        Assert.Empty(empty.Screens);
        Assert.Equal(0, empty.Version);
        Assert.Same(empty, PlatformSnapshot.Empty);
    }

    [Fact]
    public void ScreenRectComputesSize()
    {
        var rect = new ScreenRect(Left: 100, Top: 50, Right: 1380, Bottom: 770);

        Assert.Equal(1280, rect.Width);
        Assert.Equal(720, rect.Height);
    }
}
