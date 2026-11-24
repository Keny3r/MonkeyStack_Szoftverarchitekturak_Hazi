namespace CharacterEngine.Tests.Rendering;

using CharacterEngine.Rendering;
using CharacterEngine.Tests.Fakes;

public class CharacterRenderSnapshotTests
{
    [Fact]
    public void EmptySnapshot_HasDefaultValuesAndNotVisible()
    {
        var snapshot = CharacterRenderSnapshot.Empty;

        Assert.Equal(0.0, snapshot.X);
        Assert.Equal(0.0, snapshot.Y);
        Assert.False(snapshot.IsFacingLeft);
        Assert.Null(snapshot.MovementAnimation);
        Assert.Empty(snapshot.OverlayAnimations);
        Assert.False(snapshot.IsVisible);
    }

    [Fact]
    public void Snapshot_StoresPropertiesCorrectly()
    {
        var movementAnim = new FakeAnimation(64, 64);
        var overlay1 = new FakeAnimation(32, 32, 1);
        var overlay2 = new FakeAnimation(48, 48, 2);

        var snapshot = new CharacterRenderSnapshot(
            X: 150.5,
            Y: 300.0,
            IsFacingLeft: true,
            MovementAnimation: movementAnim,
            OverlayAnimations: [overlay1, overlay2],
            IsVisible: true);

        Assert.Equal(150.5, snapshot.X);
        Assert.Equal(300.0, snapshot.Y);
        Assert.True(snapshot.IsFacingLeft);
        Assert.Same(movementAnim, snapshot.MovementAnimation);
        Assert.Equal(2, snapshot.OverlayAnimations.Count);
        Assert.True(snapshot.IsVisible);
    }

    [Fact]
    public void Snapshot_WithExpression_CreatesNewModifiedInstance()
    {
        var snapshot = new CharacterRenderSnapshot(
            X: 100,
            Y: 200,
            IsFacingLeft: false,
            MovementAnimation: null,
            OverlayAnimations: [],
            IsVisible: true);

        var modified = snapshot with { X = 250, IsFacingLeft = true };

        Assert.Equal(250, modified.X);
        Assert.Equal(200, modified.Y);
        Assert.True(modified.IsFacingLeft);
        Assert.Equal(100, snapshot.X);
        Assert.False(snapshot.IsFacingLeft);
    }
}
