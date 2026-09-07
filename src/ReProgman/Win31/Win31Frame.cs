using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ReProgman.Model;

namespace ReProgman.Win31;

/// <summary>
/// The Windows 3.1 sizing frame: a 1px black outline, a gray band, a 1px black
/// inner outline, and the little notch cuts that mark the corner resize zones.
/// </summary>
public class Win31Frame : Decorator
{
    public static readonly StyledProperty<double> BandThicknessProperty =
        AvaloniaProperty.Register<Win31Frame, double>(nameof(BandThickness), 4);

    public static readonly StyledProperty<double> NotchOffsetProperty =
        AvaloniaProperty.Register<Win31Frame, double>(nameof(NotchOffset), 25);

    public static readonly StyledProperty<bool> ShowNotchesProperty =
        AvaloniaProperty.Register<Win31Frame, bool>(nameof(ShowNotches), true);

    public static readonly StyledProperty<bool> ShowFrameProperty =
        AvaloniaProperty.Register<Win31Frame, bool>(nameof(ShowFrame), true);

    /// <summary>Band color: gray for sizing frames, navy for modal dialog frames.</summary>
    public static readonly StyledProperty<IBrush> BandBrushProperty =
        AvaloniaProperty.Register<Win31Frame, IBrush>(nameof(BandBrush), Win31Palette.FaceBrush);

    /// <summary>Inner outline color: black for sizing frames, white for modal dialog frames.</summary>
    public static readonly StyledProperty<IBrush> InnerBrushProperty =
        AvaloniaProperty.Register<Win31Frame, IBrush>(nameof(InnerBrush), Win31Palette.FrameBrush);

    static Win31Frame()
    {
        AffectsRender<Win31Frame>(BandThicknessProperty, NotchOffsetProperty, ShowNotchesProperty, ShowFrameProperty, BandBrushProperty, InnerBrushProperty);
        AffectsMeasure<Win31Frame>(BandThicknessProperty, ShowFrameProperty);
    }

    public double BandThickness
    {
        get => GetValue(BandThicknessProperty);
        set => SetValue(BandThicknessProperty, value);
    }

    public double NotchOffset
    {
        get => GetValue(NotchOffsetProperty);
        set => SetValue(NotchOffsetProperty, value);
    }

    public bool ShowNotches
    {
        get => GetValue(ShowNotchesProperty);
        set => SetValue(ShowNotchesProperty, value);
    }

    public bool ShowFrame
    {
        get => GetValue(ShowFrameProperty);
        set => SetValue(ShowFrameProperty, value);
    }

    public IBrush BandBrush
    {
        get => GetValue(BandBrushProperty);
        set => SetValue(BandBrushProperty, value);
    }

    public IBrush InnerBrush
    {
        get => GetValue(InnerBrushProperty);
        set => SetValue(InnerBrushProperty, value);
    }

    /// <summary>Total frame thickness on each side (black + band + black).</summary>
    public double FrameThickness => ShowFrame ? BandThickness + 2 : 0;

    protected override Size MeasureOverride(Size availableSize)
    {
        // The child edge must land exactly on the frame's inner device boundary
        // so the inner outline keeps the same visible thickness on all four
        // sides at fractional DPI scales.
        var scale = PixelSnap.ScaleOf(this);
        Padding = new Thickness(PixelSnap.Coord(FrameThickness, scale));
        return base.MeasureOverride(availableSize);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (e.Root is TopLevel topLevel)
        {
            topLevel.ScalingChanged += OnScalingChanged;
        }

        // The DPI scale is unknown until the frame is rooted in a window.
        InvalidateMeasure();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (e.Root is TopLevel topLevel)
        {
            topLevel.ScalingChanged -= OnScalingChanged;
        }
    }

    private void OnScalingChanged(object? sender, EventArgs e) => InvalidateMeasure();

    public override void Render(DrawingContext context)
    {
        if (!ShowFrame)
        {
            return;
        }

        // Filled rectangles only, with every boundary snapped to device pixels:
        // integer-aligned fills stay crisp with no anti-aliasing artifacts,
        // unlike 1px stroked lines, at fractional DPI scales too.
        var scale = PixelSnap.ScaleOf(this);
        var width = PixelSnap.Extent(Bounds.Width, scale);
        var height = PixelSnap.Extent(Bounds.Height, scale);
        var bandEdge = Math.Round((1 + BandThickness) * scale, MidpointRounding.AwayFromZero);
        var innerEdge = Math.Round(FrameThickness * scale, MidpointRounding.AwayFromZero);
        // Both outlines are the same 1px line in the original, so the outer one
        // takes its thickness from the inner one instead of rounding the scale on
        // its own: at 150% independent rounding would give a 2px outer line
        // against a 1px inner one.
        var black = Math.Max(1, innerEdge - bandEdge);

        void Fill(IBrush brush, double x, double y, double w, double h) =>
            context.FillRectangle(brush, new Rect(x / scale, y / scale, w / scale, h / scale));

        // The inner outline is the InnerBrush margin left visible between the
        // band and the child, whose edge sits on the snapped Padding boundary.
        Fill(Win31Palette.FrameBrush, 0, 0, width, height);
        Fill(BandBrush, black, black, width - black * 2, height - black * 2);
        Fill(InnerBrush, bandEdge, bandEdge, width - bandEdge * 2, height - bandEdge * 2);

        if (!ShowNotches)
        {
            return;
        }

        var band = bandEdge - black;
        var notch = Math.Round(NotchOffset * scale, MidpointRounding.AwayFromZero);
        foreach (var y in new[] { notch, height - black - notch })
        {
            Fill(Win31Palette.FrameBrush, black, y, band, black);
            Fill(Win31Palette.FrameBrush, width - black - band, y, band, black);
        }

        foreach (var x in new[] { notch, width - black - notch })
        {
            Fill(Win31Palette.FrameBrush, x, black, black, band);
            Fill(Win31Palette.FrameBrush, x, height - black - band, black, band);
        }
    }

    /// <summary>
    /// Maps a pointer position on the frame ring to a resize direction, treating the
    /// segments outside the notches as corner zones like the original window manager.
    /// </summary>
    public ResizeEdge? HitTestEdge(Point point)
    {
        if (!ShowFrame)
        {
            return null;
        }

        var thickness = FrameThickness;
        var width = Bounds.Width;
        var height = Bounds.Height;
        var onLeft = point.X < thickness;
        var onRight = point.X >= width - thickness;
        var onTop = point.Y < thickness;
        var onBottom = point.Y >= height - thickness;
        if (!onLeft && !onRight && !onTop && !onBottom)
        {
            return null;
        }

        var offset = NotchOffset;
        var nearLeft = point.X < offset;
        var nearRight = point.X >= width - offset;
        var nearTop = point.Y < offset;
        var nearBottom = point.Y >= height - offset;

        if ((onTop && nearLeft) || (onLeft && nearTop))
        {
            return ResizeEdge.NorthWest;
        }

        if ((onTop && nearRight) || (onRight && nearTop))
        {
            return ResizeEdge.NorthEast;
        }

        if ((onBottom && nearLeft) || (onLeft && nearBottom))
        {
            return ResizeEdge.SouthWest;
        }

        if ((onBottom && nearRight) || (onRight && nearBottom))
        {
            return ResizeEdge.SouthEast;
        }

        if (onTop)
        {
            return ResizeEdge.North;
        }

        if (onBottom)
        {
            return ResizeEdge.South;
        }

        return onLeft ? ResizeEdge.West : ResizeEdge.East;
    }
}
