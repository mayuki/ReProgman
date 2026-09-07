namespace ReProgman.Model;

/// <summary>An icon's top-left position (in device-independent pixels) inside a group window.</summary>
public readonly record struct IconPosition(int X, int Y);

/// <summary>
/// Grid arithmetic for Program Manager icon cells. The cell size matches the
/// original spacing: a 32x32 icon plus a two-line label.
/// </summary>
public static class IconGridLayout
{
    public const double CellWidth = 74;
    public const double CellHeight = 62;

    public static int ColumnsFor(double availableWidth) =>
        Math.Max(1, (int)(availableWidth / CellWidth));

    public static IconPosition CellAt(int index, int columns) => new(
        (int)(index % columns * CellWidth),
        (int)(index / columns * CellHeight));

    /// <summary>Maps a dropped icon position to the row-major insertion index.</summary>
    public static int IndexAt(double x, double y, int columns, int itemCount)
    {
        var column = Math.Clamp((int)Math.Round(x / CellWidth), 0, columns - 1);
        var row = Math.Max(0, (int)Math.Round(y / CellHeight));
        return Math.Clamp(column + row * columns, 0, itemCount - 1);
    }

    /// <summary>
    /// Assigns cells to icons that have no stored position yet, skipping cells
    /// already occupied (a hand-placed icon claims its nearest grid cell).
    /// </summary>
    public static IReadOnlyList<IconPosition> PlaceMissing(
        IEnumerable<IconPosition> occupied,
        int missingCount,
        int columns)
    {
        var occupiedCells = occupied
            .Select(p =>
                Math.Clamp((int)Math.Round(p.X / CellWidth), 0, columns - 1) +
                Math.Max(0, (int)Math.Round(p.Y / CellHeight)) * columns)
            .ToHashSet();

        var result = new List<IconPosition>(missingCount);
        for (var cell = 0; result.Count < missingCount; cell++)
        {
            if (!occupiedCells.Contains(cell))
            {
                result.Add(CellAt(cell, columns));
            }
        }

        return result;
    }
}
