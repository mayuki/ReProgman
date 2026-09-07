namespace ReProgman.Model;

/// <summary>The frame edge a sizing drag grabbed, in the eight directions the original offers.</summary>
public enum ResizeEdge
{
    North,
    South,
    West,
    East,
    NorthWest,
    NorthEast,
    SouthWest,
    SouthEast,
}

/// <summary>A window rectangle in device-independent pixels.</summary>
public readonly record struct ResizeRect(double X, double Y, double Width, double Height);

/// <summary>
/// The arithmetic behind a sizing drag, shared by the main window and the MDI
/// children because Avalonia's macOS backend leaves <c>BeginResizeDrag</c> empty
/// and both have to run the drag themselves.
/// </summary>
public static class WindowResize
{
    /// <summary>
    /// Applies a pointer delta to the rectangle a drag started from. Only the
    /// grabbed edges move; shrinking past the minimum pins the grabbed edge so
    /// the opposite one never gets dragged along.
    /// </summary>
    public static ResizeRect Drag(
        ResizeRect start,
        ResizeEdge edge,
        double deltaX,
        double deltaY,
        double minWidth,
        double minHeight)
    {
        double x = start.X, y = start.Y, width = start.Width, height = start.Height;

        if (edge is ResizeEdge.West or ResizeEdge.NorthWest or ResizeEdge.SouthWest)
        {
            var newWidth = Math.Max(minWidth, width - deltaX);
            x += width - newWidth;
            width = newWidth;
        }

        if (edge is ResizeEdge.East or ResizeEdge.NorthEast or ResizeEdge.SouthEast)
        {
            width = Math.Max(minWidth, width + deltaX);
        }

        if (edge is ResizeEdge.North or ResizeEdge.NorthWest or ResizeEdge.NorthEast)
        {
            var newHeight = Math.Max(minHeight, height - deltaY);
            y += height - newHeight;
            height = newHeight;
        }

        if (edge is ResizeEdge.South or ResizeEdge.SouthWest or ResizeEdge.SouthEast)
        {
            height = Math.Max(minHeight, height + deltaY);
        }

        return new ResizeRect(x, y, width, height);
    }
}
