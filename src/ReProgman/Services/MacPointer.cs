using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia;

namespace ReProgman.Services;

/// <summary>
/// The pointer position as the window server sees it, in the global
/// top-left-origin point coordinates a window's Position is expressed in.
/// A sizing drag cannot use the position carried by the input events instead:
/// those are relative to the window being dragged, and a west or north drag
/// moves that window, so the reference they are measured against shifts under
/// them and the drag oscillates.
/// </summary>
[SupportedOSPlatform("macos")]
public static class MacPointer
{
    private const string CoreGraphics = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    [StructLayout(LayoutKind.Sequential)]
    private struct CGPoint
    {
        public double X;
        public double Y;
    }

    /// <summary>A null source asks for an event describing the current input state.</summary>
    [DllImport(CoreGraphics)]
    private static extern IntPtr CGEventCreate(IntPtr source);

    [DllImport(CoreGraphics)]
    private static extern CGPoint CGEventGetLocation(IntPtr handle);

    [DllImport(CoreFoundation)]
    private static extern void CFRelease(IntPtr handle);

    public static bool TryGetScreenPosition(out Point position)
    {
        var handle = CGEventCreate(IntPtr.Zero);
        if (handle == IntPtr.Zero)
        {
            position = default;
            return false;
        }

        try
        {
            var location = CGEventGetLocation(handle);
            position = new Point(location.X, location.Y);
            return true;
        }
        finally
        {
            CFRelease(handle);
        }
    }
}
