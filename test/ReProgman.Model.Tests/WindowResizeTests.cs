using ReProgman.Model;

namespace ReProgman.Model.Tests;

public class WindowResizeTests
{
    private static readonly ResizeRect Start = new(100, 50, 400, 300);

    [Fact]
    public void EastDragGrowsTheWidthAndLeavesTheOriginAlone()
    {
        var rect = WindowResize.Drag(Start, ResizeEdge.East, 30, 999, 200, 100);

        Assert.Equal(new ResizeRect(100, 50, 430, 300), rect);
    }

    [Fact]
    public void SouthDragGrowsTheHeightAndLeavesTheOriginAlone()
    {
        var rect = WindowResize.Drag(Start, ResizeEdge.South, 999, 40, 200, 100);

        Assert.Equal(new ResizeRect(100, 50, 400, 340), rect);
    }

    [Fact]
    public void WestDragMovesTheLeftEdgeAndKeepsTheRightEdgeInPlace()
    {
        var rect = WindowResize.Drag(Start, ResizeEdge.West, 30, 0, 200, 100);

        Assert.Equal(new ResizeRect(130, 50, 370, 300), rect);
        Assert.Equal(Start.X + Start.Width, rect.X + rect.Width);
    }

    [Fact]
    public void NorthDragMovesTheTopEdgeAndKeepsTheBottomEdgeInPlace()
    {
        var rect = WindowResize.Drag(Start, ResizeEdge.North, 0, -20, 200, 100);

        Assert.Equal(new ResizeRect(100, 30, 400, 320), rect);
        Assert.Equal(Start.Y + Start.Height, rect.Y + rect.Height);
    }

    [Fact]
    public void NorthWestDragMovesBothEdges()
    {
        var rect = WindowResize.Drag(Start, ResizeEdge.NorthWest, -10, -20, 200, 100);

        Assert.Equal(new ResizeRect(90, 30, 410, 320), rect);
    }

    [Fact]
    public void SouthEastDragGrowsBothSides()
    {
        var rect = WindowResize.Drag(Start, ResizeEdge.SouthEast, 10, 20, 200, 100);

        Assert.Equal(new ResizeRect(100, 50, 410, 320), rect);
    }

    [Fact]
    public void ShrinkingPastTheMinimumPinsTheGrabbedEdge()
    {
        // Overshooting the minimum must not drag the fixed edge along: the right
        // edge stays put and the left edge stops at the minimum width.
        var rect = WindowResize.Drag(Start, ResizeEdge.NorthWest, 900, 900, 200, 100);

        Assert.Equal(new ResizeRect(300, 250, 200, 100), rect);
        Assert.Equal(Start.X + Start.Width, rect.X + rect.Width);
        Assert.Equal(Start.Y + Start.Height, rect.Y + rect.Height);
    }

    [Fact]
    public void ShrinkingPastTheMinimumFromTheOppositeEdgeKeepsTheOrigin()
    {
        var rect = WindowResize.Drag(Start, ResizeEdge.SouthEast, -900, -900, 200, 100);

        Assert.Equal(new ResizeRect(100, 50, 200, 100), rect);
    }
}
