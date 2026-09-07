using Avalonia.Media;

namespace ReProgman.Win31;

/// <summary>The fixed Windows 3.1 default color scheme ("Windows Default").</summary>
public static class Win31Palette
{
    public static readonly Color Face = Color.FromRgb(0xC0, 0xC0, 0xC0);
    public static readonly Color Shadow = Color.FromRgb(0x80, 0x80, 0x80);
    public static readonly Color Highlight = Colors.White;
    public static readonly Color Frame = Colors.Black;
    public static readonly Color ActiveTitle = Color.FromRgb(0x00, 0x00, 0xB8);

    public static readonly IBrush FaceBrush = new SolidColorBrush(Face).ToImmutable();
    public static readonly IBrush ShadowBrush = new SolidColorBrush(Shadow).ToImmutable();
    public static readonly IBrush HighlightBrush = new SolidColorBrush(Highlight).ToImmutable();
    public static readonly IBrush FrameBrush = new SolidColorBrush(Frame).ToImmutable();
    public static readonly IBrush ActiveTitleBrush = new SolidColorBrush(ActiveTitle).ToImmutable();
}
