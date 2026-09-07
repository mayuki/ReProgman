using ReProgman.Model;

namespace ReProgman.Model.Tests;

public class IconGridLayoutTests
{
    [Theory]
    [InlineData(300, 4)]
    [InlineData(148, 2)]
    [InlineData(74, 1)]
    [InlineData(73, 1)]
    [InlineData(0, 1)]
    [InlineData(-10, 1)]
    public void ColumnsFor_DividesWidthIntoCells(double width, int expected)
    {
        Assert.Equal(expected, IconGridLayout.ColumnsFor(width));
    }

    [Theory]
    [InlineData(0, 3, 0, 0)]
    [InlineData(2, 3, 148, 0)]
    [InlineData(3, 3, 0, 62)]
    [InlineData(7, 3, 74, 124)]
    public void CellAt_LaysOutRowMajor(int index, int columns, int x, int y)
    {
        Assert.Equal(new IconPosition(x, y), IconGridLayout.CellAt(index, columns));
    }

    [Theory]
    [InlineData(80, 10, 3, 10, 1)]     // second column, first row
    [InlineData(500, 10, 3, 10, 2)]    // beyond the last column clamps to it
    [InlineData(10, 130, 3, 10, 6)]    // third row
    [InlineData(160, 130, 3, 8, 7)]    // beyond the item count clamps to the last item
    [InlineData(-30, -30, 3, 10, 0)]   // negative coordinates clamp to the first cell
    public void IndexAt_MapsPositionToInsertionIndex(double x, double y, int columns, int count, int expected)
    {
        Assert.Equal(expected, IconGridLayout.IndexAt(x, y, columns, count));
    }

    [Fact]
    public void PlaceMissing_FillsFreeCellsInRowMajorOrder()
    {
        var occupied = new[] { new IconPosition(0, 0), new IconPosition(74, 0) };

        var placed = IconGridLayout.PlaceMissing(occupied, missingCount: 3, columns: 3);

        Assert.Equal(
            [new IconPosition(148, 0), new IconPosition(0, 62), new IconPosition(74, 62)],
            placed);
    }

    [Fact]
    public void PlaceMissing_TreatsNearbyPositionsAsTheSameCell()
    {
        // A hand-dragged icon sitting slightly off-grid still occupies its nearest cell.
        var occupied = new[] { new IconPosition(5, 3) };

        var placed = IconGridLayout.PlaceMissing(occupied, missingCount: 1, columns: 2);

        Assert.Equal([new IconPosition(74, 0)], placed);
    }

    [Fact]
    public void PlaceMissing_WithNoOccupiedCells_StartsAtOrigin()
    {
        var placed = IconGridLayout.PlaceMissing([], missingCount: 2, columns: 4);

        Assert.Equal([new IconPosition(0, 0), new IconPosition(74, 0)], placed);
    }
}
