namespace ConfigurationEngine.Tests.Media;

using ConfigurationEngine.Media;

public sealed class NaturalFileNameComparerTests
{
    [Fact]
    public void Numbers_AreComparedByValue()
    {
        string[] files = [@"C:\a\frame10.png", @"C:\a\Frame2.png", @"C:\a\frame1.png", @"C:\a\frame002b.png", @"C:\a\a.png"];

        var sorted = files.Order(NaturalFileNameComparer.Instance).Select(System.IO.Path.GetFileName);

        Assert.Equal(["a.png", "frame1.png", "Frame2.png", "frame002b.png", "frame10.png"], sorted);
    }
}
