using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace ReProgman.Win31;

public enum BevelMode
{
    None,
    Raised,
    Pressed,
}

/// <summary>
/// Draws the classic Windows 3.1 3D edge: white highlight on the top/left,
/// dark gray shadow on the bottom/right, with the shadow owning the corners.
/// </summary>
public class Bevel : Decorator
{
    public static readonly StyledProperty<BevelMode> ModeProperty =
        AvaloniaProperty.Register<Bevel, BevelMode>(nameof(Mode), BevelMode.Raised);

    public static readonly StyledProperty<int> DepthProperty =
        AvaloniaProperty.Register<Bevel, int>(nameof(Depth), 2);

    /// <summary>
    /// Highlight thickness; -1 means "same as Depth". Caption and scroll bar
    /// buttons use a 1px highlight with a 2px shadow, as measured on the original.
    /// </summary>
    public static readonly StyledProperty<int> HighlightDepthProperty =
        AvaloniaProperty.Register<Bevel, int>(nameof(HighlightDepth), -1);

    static Bevel()
    {
        AffectsRender<Bevel>(ModeProperty, DepthProperty, HighlightDepthProperty);
        AffectsMeasure<Bevel>(DepthProperty);
    }

    public BevelMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public int Depth
    {
        get => GetValue(DepthProperty);
        set => SetValue(DepthProperty, value);
    }

    public int HighlightDepth
    {
        get => GetValue(HighlightDepthProperty);
        set => SetValue(HighlightDepthProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // The edge thickness is constant regardless of mode so that toggling
        // Raised/Pressed on a button press does not shift the layout.
        Padding = new Thickness(Depth);
        return base.MeasureOverride(availableSize);
    }

    public override void Render(DrawingContext context)
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0 || Mode == BevelMode.None)
        {
            return;
        }

        // Drawn in device pixels: each nominal 1px ring runs between snapped
        // device boundaries so the edges stay uniform at fractional DPI scales.
        var scale = PixelSnap.ScaleOf(this);
        var width = PixelSnap.Extent(Bounds.Width, scale);
        var height = PixelSnap.Extent(Bounds.Height, scale);

        double Boundary(int i) => Math.Round(i * scale, MidpointRounding.AwayFromZero);

        void Fill(IBrush brush, double x, double y, double w, double h) =>
            context.FillRectangle(brush, new Rect(x / scale, y / scale, w / scale, h / scale));

        if (Mode == BevelMode.Pressed)
        {
            // A pressed control shows only a thin shadow along the top/left edge.
            var thickness = Boundary(1);
            Fill(Win31Palette.ShadowBrush, 0, 0, width, thickness);
            Fill(Win31Palette.ShadowBrush, 0, 0, thickness, height);
            return;
        }

        var highlightDepth = HighlightDepth < 0 ? Depth : HighlightDepth;
        for (var i = 0; i < Depth; i++)
        {
            var inner = Boundary(i);
            var outer = Boundary(i + 1);
            var step = outer - inner;

            // Shadow first so the highlight never bleeds into the bottom-right corners.
            Fill(Win31Palette.ShadowBrush, inner, height - outer, width - inner * 2, step);
            Fill(Win31Palette.ShadowBrush, width - outer, inner, step, height - inner * 2);
            if (i < highlightDepth)
            {
                Fill(Win31Palette.HighlightBrush, inner, inner, width - inner * 2 - step, step);
                Fill(Win31Palette.HighlightBrush, inner, inner, step, height - inner * 2 - step);
            }
        }
    }
}
