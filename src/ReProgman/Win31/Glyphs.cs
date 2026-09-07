using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace ReProgman.Win31;

/// <summary>The wide "minus" bar shown inside a system menu box.</summary>
public class SystemBoxGlyph : Control
{
    /// <summary>Outer bar width including the black outline: 13 for top-level windows, 7 for MDI children.</summary>
    public static readonly StyledProperty<double> BarWidthProperty =
        AvaloniaProperty.Register<SystemBoxGlyph, double>(nameof(BarWidth), 13);

    static SystemBoxGlyph()
    {
        AffectsRender<SystemBoxGlyph>(BarWidthProperty);
    }

    public double BarWidth
    {
        get => GetValue(BarWidthProperty);
        set => SetValue(BarWidthProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        // Fill the whole bounds so the entire box is hit-testable: the light
        // dismiss pass-through check only forwards presses that hit a DESCENDANT
        // of the pass-through element, so hits on the container border itself
        // are swallowed and a double-click would only work on the bar's pixels.
        context.FillRectangle(Win31Palette.FaceBrush, new Rect(Bounds.Size));

        // Measured from the original: a BarWidth x 3 black box around a 1px-high
        // white face, with a 1px gray drop shadow offset by (+1,+1). Laid out in
        // DIPs (which match the original's pixels) with every edge snapped to the
        // device grid, so the bar keeps the same share of the box at any scale.
        var scale = PixelSnap.ScaleOf(this);
        var barWidth = BarWidth;
        var x = Math.Floor((Bounds.Width - barWidth - 1) / 2);
        var y = Math.Floor((Bounds.Height - 3) / 2);

        void Fill(IBrush brush, double left, double top, double w, double h) =>
            PixelSnap.FillSnapped(context, brush, scale, left, top, w, h);

        Fill(Win31Palette.ShadowBrush, x + 1, y + 1, barWidth, 3);
        Fill(Win31Palette.FrameBrush, x, y, barWidth, 3);
        Fill(Win31Palette.HighlightBrush, x + 1, y + 1, barWidth - 2, 1);
    }
}

public enum ArrowKind
{
    Up,
    Down,
    Left,
    Right,
    UpDown,
}

/// <summary>
/// Solid black arrows used by caption buttons, scroll bars, and menus, drawn as
/// stacked pixel rows so the diagonals stay razor sharp. Measured from the
/// original: caption arrows are a 4-row head (widths 1,3,5,7); scroll bar
/// arrows add a 3px-wide shaft below the head.
/// </summary>
public class ArrowGlyph : Control
{
    private const int HeadSize = 4;
    private const int ShaftSize = 3;

    public static readonly StyledProperty<ArrowKind> KindProperty =
        AvaloniaProperty.Register<ArrowGlyph, ArrowKind>(nameof(Kind));

    public static readonly StyledProperty<bool> ShaftProperty =
        AvaloniaProperty.Register<ArrowGlyph, bool>(nameof(Shaft));

    public static readonly StyledProperty<IBrush> FillProperty =
        AvaloniaProperty.Register<ArrowGlyph, IBrush>(nameof(Fill), Win31Palette.FrameBrush);

    static ArrowGlyph()
    {
        AffectsRender<ArrowGlyph>(KindProperty, ShaftProperty, FillProperty);
    }

    public ArrowKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    /// <summary>Adds the scroll-bar style shaft behind the arrow head.</summary>
    public bool Shaft
    {
        get => GetValue(ShaftProperty);
        set => SetValue(ShaftProperty, value);
    }

    public IBrush Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    // Device pixels per DIP for the current render pass. The arrow is laid out in
    // DIPs (which match the original's pixels) and every row is snapped to the
    // device grid, so it keeps its size inside the button at any scale.
    private double _scale = 1;

    public override void Render(DrawingContext context)
    {
        _scale = PixelSnap.ScaleOf(this);
        var width = Bounds.Width;
        var height = Bounds.Height;
        var centerX = Math.Floor((width - 1) / 2);
        var centerY = Math.Floor((height - 1) / 2);
        var arrowLength = Shaft ? HeadSize + ShaftSize : HeadSize;

        switch (Kind)
        {
            case ArrowKind.Up:
            {
                // A lone maximize arrow sits one pixel above the minimize arrow's
                // position on the original caption buttons.
                var top = Math.Floor((height - arrowLength) / 2) - (Shaft ? 0 : 1);
                DrawHeadUp(context, centerX, top);
                if (Shaft)
                {
                    FillRow(context, centerX - 1, top + HeadSize, 3, ShaftSize);
                }

                break;
            }

            case ArrowKind.Down:
            {
                var top = Math.Floor((height - arrowLength) / 2);
                if (Shaft)
                {
                    FillRow(context, centerX - 1, top, 3, ShaftSize);
                    DrawHeadDown(context, centerX, top + ShaftSize);
                }
                else
                {
                    DrawHeadDown(context, centerX, top);
                }

                break;
            }

            case ArrowKind.UpDown:
            {
                // The restore mark: both heads stacked with a 1px gap.
                var top = Math.Floor((height - (HeadSize * 2 + 1)) / 2);
                DrawHeadUp(context, centerX, top);
                DrawHeadDown(context, centerX, top + HeadSize + 1);
                break;
            }

            case ArrowKind.Left:
            {
                var left = Math.Floor((width - arrowLength) / 2);
                DrawHeadLeft(context, left, centerY);
                if (Shaft)
                {
                    FillRow(context, left + HeadSize, centerY - 1, ShaftSize, 3);
                }

                break;
            }

            case ArrowKind.Right:
            {
                var left = Math.Floor((width - arrowLength) / 2);
                if (Shaft)
                {
                    FillRow(context, left, centerY - 1, ShaftSize, 3);
                    DrawHeadRight(context, left + ShaftSize, centerY);
                }
                else
                {
                    DrawHeadRight(context, left, centerY);
                }

                break;
            }
        }
    }

    private void FillRow(DrawingContext context, double x, double y, double w, double h) =>
        PixelSnap.FillSnapped(context, Fill, _scale, x, y, w, h);

    private void DrawHeadUp(DrawingContext context, double centerX, double top)
    {
        for (var i = 0; i < HeadSize; i++)
        {
            FillRow(context, centerX - i, top + i, i * 2 + 1, 1);
        }
    }

    private void DrawHeadDown(DrawingContext context, double centerX, double top)
    {
        for (var i = 0; i < HeadSize; i++)
        {
            var half = HeadSize - 1 - i;
            FillRow(context, centerX - half, top + i, half * 2 + 1, 1);
        }
    }

    private void DrawHeadLeft(DrawingContext context, double left, double centerY)
    {
        for (var i = 0; i < HeadSize; i++)
        {
            FillRow(context, left + i, centerY - i, 1, i * 2 + 1);
        }
    }

    private void DrawHeadRight(DrawingContext context, double left, double centerY)
    {
        for (var i = 0; i < HeadSize; i++)
        {
            var half = HeadSize - 1 - i;
            FillRow(context, left + i, centerY - half, 1, half * 2 + 1);
        }
    }
}

/// <summary>
/// Hand-drawn stand-in for the Windows 3.1 information icon: a blue circle with
/// a white serif "i". Drawn from scratch to avoid copying Microsoft art.
/// </summary>
public class InfoIconArt : Control
{
    private static readonly IBrush Blue = new SolidColorBrush(Color.FromRgb(0x00, 0x00, 0xFF)).ToImmutable();

    public override void Render(DrawingContext context)
    {
        var deviceScale = PixelSnap.ScaleOf(this);
        var unit = Math.Min(Bounds.Width, Bounds.Height) / 32.0;

        void Fill(IBrush brush, int x, int y, int w, int h) =>
            PixelSnap.FillArtCell(context, brush, unit, deviceScale, x, y, w, h);

        // A stepped pixel circle reads as the aliased original at small sizes.
        int[][] spans = [[10, 12], [7, 18], [5, 22], [4, 24], [3, 26], [2, 28], [2, 28], [1, 30]];
        for (var i = 0; i < spans.Length; i++)
        {
            Fill(Blue, spans[i][0], i, spans[i][1], 1);
            Fill(Blue, spans[i][0], 31 - i, spans[i][1], 1);
        }

        Fill(Blue, 0, 8, 32, 16);

        // The serif "i": dot, top serif, stem, base serif.
        Fill(Win31Palette.HighlightBrush, 13, 4, 6, 6);
        Fill(Win31Palette.HighlightBrush, 11, 12, 8, 2);
        Fill(Win31Palette.HighlightBrush, 13, 14, 6, 10);
        Fill(Win31Palette.HighlightBrush, 10, 24, 12, 3);
    }
}

/// <summary>
/// Hand-drawn stand-in for the Program Manager group icon: a tiny document window
/// full of colorful program icons. Drawn from scratch to avoid copying Microsoft art.
/// </summary>
public class GroupIconArt : Control
{
    private static readonly IBrush[] ItemColors =
    [
        new SolidColorBrush(Color.FromRgb(0xFF, 0x00, 0x00)).ToImmutable(),
        new SolidColorBrush(Color.FromRgb(0x00, 0x80, 0x00)).ToImmutable(),
        new SolidColorBrush(Color.FromRgb(0x00, 0x00, 0xFF)).ToImmutable(),
        new SolidColorBrush(Color.FromRgb(0x00, 0x80, 0x80)).ToImmutable(),
        new SolidColorBrush(Color.FromRgb(0xFF, 0x00, 0xFF)).ToImmutable(),
        new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x00)).ToImmutable(),
    ];

    public override void Render(DrawingContext context)
    {
        var deviceScale = PixelSnap.ScaleOf(this);
        var unit = Math.Min(Bounds.Width, Bounds.Height) / 32.0;

        void Fill(IBrush brush, int x, int y, int w, int h) =>
            PixelSnap.FillArtCell(context, brush, unit, deviceScale, x, y, w, h);

        // Window with a navy caption bar.
        Fill(Win31Palette.FrameBrush, 1, 4, 30, 24);
        Fill(Win31Palette.ActiveTitleBrush, 2, 5, 28, 4);
        Fill(Win31Palette.HighlightBrush, 2, 9, 28, 18);

        // Two rows of colorful "program icons" with label lines underneath.
        for (var index = 0; index < ItemColors.Length; index++)
        {
            var x = 5 + (index % 3) * 9;
            var y = 11 + (index / 3) * 8;
            Fill(ItemColors[index], x, y, 5, 4);
            Fill(Win31Palette.FrameBrush, x, y + 5, 5, 1);
        }
    }

    /// <summary>
    /// Draws the art into a square bitmap. Used for the window icon and, at the
    /// larger sizes, for the icon of the macOS application bundle.
    /// </summary>
    public static RenderTargetBitmap RenderToBitmap(int size)
    {
        var art = new GroupIconArt { Width = size, Height = size };
        art.Measure(new Size(size, size));
        art.Arrange(new Rect(0, 0, size, size));
        var bitmap = new RenderTargetBitmap(new PixelSize(size, size), new Vector(96, 96));
        bitmap.Render(art);
        return bitmap;
    }
}
