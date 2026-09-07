using ReProgman.Model;

namespace ReProgman.Model.Tests;

public class WindowPlacementTests
{
    [Fact]
    public void ToIniString_FormatsAllFields()
    {
        var placement = new WindowPlacement(10, -20, 300, 200, WindowStateKind.Minimized);

        Assert.Equal("10,-20,300,200,minimized", placement.ToIniString());
    }

    [Theory]
    [InlineData("10,20,300,200,normal", 10, 20, 300, 200, WindowStateKind.Normal)]
    [InlineData("0,0,1,1,maximized", 0, 0, 1, 1, WindowStateKind.Maximized)]
    [InlineData(" 10 , 20 , 300 , 200 , minimized ", 10, 20, 300, 200, WindowStateKind.Minimized)]
    public void TryParse_ReadsValidStrings(string text, int x, int y, int w, int h, WindowStateKind state)
    {
        Assert.True(WindowPlacement.TryParse(text, out var placement));
        Assert.Equal(new WindowPlacement(x, y, w, h, state), placement);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1,2,3,4")]
    [InlineData("1,2,3,4,unknown")]
    [InlineData("a,b,c,d,normal")]
    [InlineData("1,2,3,4,normal,5")]
    public void TryParse_RejectsInvalidStrings(string text)
    {
        Assert.False(WindowPlacement.TryParse(text, out _));
    }
}
