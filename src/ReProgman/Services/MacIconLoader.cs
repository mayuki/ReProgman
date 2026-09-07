using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ReProgman.Model;

namespace ReProgman.Services;

/// <summary>
/// Reads the 32x32 icon of a macOS application bundle from its .icns resource.
/// Everything up to the pixels is plain managed code in ReProgman.Model, so no
/// AppKit interop is involved and the app stays NativeAOT friendly.
/// </summary>
[SupportedOSPlatform("macos")]
public static class MacIconLoader
{
    private const int IconSize = 32;

    public static Bitmap? GetLargeIcon(string path)
    {
        if (MacAppBundle.FindIconFile(path) is not { } iconFile)
        {
            return null;
        }

        try
        {
            var image = IcnsDecoder.Decode(File.ReadAllBytes(iconFile), IconSize);
            return image?.Format switch
            {
                IcnsImageFormat.Png => new Bitmap(new MemoryStream(image.Data)),
                IcnsImageFormat.Bgra32 => FromPixels(image),
                _ => null,
            };
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // A bundle on an unmounted volume must not take the whole group down;
            // the item simply falls back to the generic icon.
            return null;
        }
    }

    private static Bitmap FromPixels(IcnsImage image)
    {
        var bitmap = new WriteableBitmap(
            new PixelSize(image.Width, image.Height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Unpremul);
        using (var buffer = bitmap.Lock())
        {
            for (var y = 0; y < image.Height; y++)
            {
                Marshal.Copy(image.Data, y * image.Width * 4, buffer.Address + (y * buffer.RowBytes), image.Width * 4);
            }
        }

        return bitmap;
    }
}
