using Avalonia;
using Avalonia.Media;
using Avalonia.Rendering;
using Avalonia.VisualTree;

namespace ReProgman.Win31;

/// <summary>
/// Helpers for drawing the hand-made chrome on the device pixel grid. Layout
/// runs in DIPs that match the original's pixels one to one, so at fractional
/// DPI scales (125%, 150%) a naive 1px rectangle straddles device pixels and the
/// aliased fill picks uneven widths. Snapping every drawn boundary to the device
/// grid keeps lines crisp at any scale while the art keeps its size relative to
/// the control it sits in.
/// </summary>
internal static class PixelSnap
{
    /// <summary>
    /// Forced scale for the screenshot debug mode: it renders into a 96-DPI
    /// bitmap regardless of the monitor the window happens to be on, so the
    /// window's RenderScaling would not match the actual render target.
    /// </summary>
    public static double? Override { get; set; }

    /// <summary>Device pixels per DIP for the window the visual lives in.</summary>
    public static double ScaleOf(Visual visual) =>
        Override ?? (visual.GetVisualRoot() as IRenderRoot)?.RenderScaling ?? 1.0;

    /// <summary>Snaps a DIP coordinate onto the device pixel grid.</summary>
    public static double Coord(double dips, double scale) =>
        Math.Round(dips * scale, MidpointRounding.AwayFromZero) / scale;

    /// <summary>
    /// A control's drawable extent in whole device pixels. Floored, because a
    /// layout may hand out fractional DIP sizes and the final half pixel was
    /// never painted by the aliased fills either; the epsilon absorbs the
    /// floating point noise of DIP-times-scale products.
    /// </summary>
    public static double Extent(double dips, double scale) =>
        Math.Floor(dips * scale + 0.001);

    /// <summary>
    /// Fills a rectangle given in DIPs, snapping each boundary to device pixels
    /// so the shape keeps its size relative to the control and adjacent parts
    /// stay seamless.
    /// </summary>
    public static void FillSnapped(DrawingContext context, IBrush brush, double scale, double x, double y, double w, double h) =>
        FillArtCell(context, brush, 1.0, scale, x, y, w, h);

    /// <summary>
    /// Fills one cell of an art grid, snapping each boundary to device pixels so
    /// adjacent cells stay seamless. <paramref name="unit"/> is the DIP size of
    /// one art pixel.
    /// </summary>
    public static void FillArtCell(DrawingContext context, IBrush brush, double unit, double scale, double x, double y, double w, double h)
    {
        var x0 = Math.Round(x * unit * scale, MidpointRounding.AwayFromZero);
        var y0 = Math.Round(y * unit * scale, MidpointRounding.AwayFromZero);
        var x1 = Math.Round((x + w) * unit * scale, MidpointRounding.AwayFromZero);
        var y1 = Math.Round((y + h) * unit * scale, MidpointRounding.AwayFromZero);
        context.FillRectangle(brush, new Rect(x0 / scale, y0 / scale, (x1 - x0) / scale, (y1 - y0) / scale));
    }
}
