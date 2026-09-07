namespace ReProgman.Model;

/// <summary>
/// Alpha-channel helpers for 32bpp BGRA icon pixel buffers. Legacy icons carry
/// no alpha in their color data and rely on a separate AND mask instead.
/// </summary>
public static class IconPixels
{
    public static bool HasAlpha(ReadOnlySpan<byte> bgra)
    {
        for (var i = 3; i < bgra.Length; i += 4)
        {
            if (bgra[i] != 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Derives alpha from an AND mask rendered as 32bpp: black (0) keeps the
    /// pixel opaque, anything else makes it transparent.
    /// </summary>
    public static void ApplyMask(Span<byte> colorBgra, ReadOnlySpan<byte> maskBgra)
    {
        for (var i = 0; i + 3 < colorBgra.Length && i + 2 < maskBgra.Length; i += 4)
        {
            var opaque = maskBgra[i] == 0 && maskBgra[i + 1] == 0 && maskBgra[i + 2] == 0;
            colorBgra[i + 3] = opaque ? (byte)255 : (byte)0;
        }
    }

    public static void MakeOpaque(Span<byte> bgra)
    {
        for (var i = 3; i < bgra.Length; i += 4)
        {
            bgra[i] = 255;
        }
    }
}
