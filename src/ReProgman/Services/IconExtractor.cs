using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ReProgman.Model;

namespace ReProgman.Services;

/// <summary>
/// Extracts 32x32 shell icons for shortcut files. SHGetFileInfo resolves .lnk targets
/// by itself, so no explicit shortcut parsing is needed to show the right icon.
/// The HICON is converted to a bitmap with plain GDI calls (GetIconInfo/GetDIBits)
/// so that no System.Drawing dependency is required — this keeps the app NativeAOT
/// compatible.
/// </summary>
[SupportedOSPlatform("windows")]
public static class IconExtractor
{
    private const uint ShgfiIcon = 0x000000100;
    private const uint ShgfiLargeIcon = 0x000000000;
    private const uint ShgfiSysIconIndex = 0x000004000;
    private const uint IldNormal = 0x00000000;

    public static Bitmap? GetLargeIcon(string path) =>
        GetIconWithoutOverlay(path) ?? GetIconWithOverlay(path);

    /// <summary>
    /// Takes the icon straight out of the system image list. Asking for the icon
    /// of a .lnk composites the shortcut arrow into it, which Program Manager
    /// items never had; the image list holds the plain icon because the shell
    /// draws overlays separately.
    /// </summary>
    private static Bitmap? GetIconWithoutOverlay(string path)
    {
        var info = default(ShFileInfo);
        var imageList = SHGetFileInfoW(path, 0, ref info, (uint)Marshal.SizeOf<ShFileInfo>(), ShgfiSysIconIndex | ShgfiLargeIcon);
        if (imageList == IntPtr.Zero)
        {
            return null;
        }

        // The system image list belongs to the shell and must not be destroyed.
        var hIcon = ImageList_GetIcon(imageList, info.iIcon, IldNormal);
        if (hIcon == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            return IconToBitmap(hIcon);
        }
        catch
        {
            return null;
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    private static Bitmap? GetIconWithOverlay(string path)
    {
        var info = default(ShFileInfo);
        var result = SHGetFileInfoW(path, 0, ref info, (uint)Marshal.SizeOf<ShFileInfo>(), ShgfiIcon | ShgfiLargeIcon);
        if (result == IntPtr.Zero || info.hIcon == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            return IconToBitmap(info.hIcon);
        }
        catch
        {
            // A broken shortcut target must not take the whole group down; the item
            // simply falls back to the generic icon.
            return null;
        }
        finally
        {
            DestroyIcon(info.hIcon);
        }
    }

    private static Bitmap? IconToBitmap(IntPtr hIcon)
    {
        if (!GetIconInfo(hIcon, out var iconInfo))
        {
            return null;
        }

        try
        {
            // Monochrome icons have no color bitmap; those are rare enough that the
            // generic group icon is an acceptable substitute.
            if (iconInfo.hbmColor == IntPtr.Zero ||
                GetObjectW(iconInfo.hbmColor, Marshal.SizeOf<BitmapStruct>(), out BitmapStruct bitmapInfo) == 0)
            {
                return null;
            }

            var width = bitmapInfo.bmWidth;
            var height = Math.Abs(bitmapInfo.bmHeight);
            if (width <= 0 || height <= 0 || width > 512 || height > 512)
            {
                return null;
            }

            var color = GetBitmapPixels(iconInfo.hbmColor, width, height);
            if (color is null)
            {
                return null;
            }

            if (!IconPixels.HasAlpha(color))
            {
                var mask = iconInfo.hbmMask != IntPtr.Zero ? GetBitmapPixels(iconInfo.hbmMask, width, height) : null;
                if (mask is not null)
                {
                    IconPixels.ApplyMask(color, mask);
                }
                else
                {
                    IconPixels.MakeOpaque(color);
                }
            }

            var bitmap = new WriteableBitmap(
                new PixelSize(width, height),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Unpremul);
            using (var buffer = bitmap.Lock())
            {
                for (var y = 0; y < height; y++)
                {
                    Marshal.Copy(color, y * width * 4, buffer.Address + y * buffer.RowBytes, width * 4);
                }
            }

            return bitmap;
        }
        finally
        {
            if (iconInfo.hbmColor != IntPtr.Zero)
            {
                DeleteObject(iconInfo.hbmColor);
            }

            if (iconInfo.hbmMask != IntPtr.Zero)
            {
                DeleteObject(iconInfo.hbmMask);
            }
        }
    }

    /// <summary>Reads a GDI bitmap as top-down 32bpp BGRA (GDI converts mono masks for us).</summary>
    private static byte[]? GetBitmapPixels(IntPtr hBitmap, int width, int height)
    {
        var header = new BitmapInfoHeader
        {
            biSize = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
            biWidth = width,
            biHeight = -height,
            biPlanes = 1,
            biBitCount = 32,
            biCompression = 0,
        };

        var pixels = new byte[width * height * 4];
        var dc = GetDC(IntPtr.Zero);
        try
        {
            return GetDIBits(dc, hBitmap, 0, (uint)height, pixels, ref header, 0) == height ? pixels : null;
        }
        finally
        {
            ReleaseDC(IntPtr.Zero, dc);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileInfo
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IconInfoStruct
    {
        public int fIcon;
        public int xHotspot;
        public int yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapStruct
    {
        public int bmType;
        public int bmWidth;
        public int bmHeight;
        public int bmWidthBytes;
        public ushort bmPlanes;
        public ushort bmBitsPixel;
        public IntPtr bmBits;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfoW(string pszPath, uint dwFileAttributes, ref ShFileInfo psfi, uint cbFileInfo, uint uFlags);

    [DllImport("comctl32.dll")]
    private static extern IntPtr ImageList_GetIcon(IntPtr himl, int i, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetIconInfo(IntPtr hIcon, out IconInfoStruct piconinfo);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll", EntryPoint = "GetObjectW")]
    private static extern int GetObjectW(IntPtr h, int c, out BitmapStruct pv);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr ho);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(IntPtr hdc, IntPtr hbm, uint start, uint cLines, byte[] lpvBits, ref BitmapInfoHeader lpbmi, uint usage);
}
