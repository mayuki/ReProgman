using Avalonia.Media.Imaging;

namespace ReProgman.Services;

/// <summary>
/// Platform entry point for turning a program item path into its 32x32 icon.
/// Windows reads the shell icon of the shortcut, macOS decodes the .icns of the
/// application bundle. A null result leaves the hand-drawn group icon in place.
/// </summary>
public static class IconLoader
{
    public static Bitmap? GetLargeIcon(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return IconExtractor.GetLargeIcon(path);
        }

        if (OperatingSystem.IsMacOS())
        {
            return MacIconLoader.GetLargeIcon(path);
        }

        return null;
    }
}
