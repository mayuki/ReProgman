using ReProgman.Model;

namespace ReProgman.Model.Tests;

public class IconPixelsTests
{
    [Fact]
    public void HasAlpha_FalseWhenEveryAlphaByteIsZero()
    {
        byte[] bgra = [10, 20, 30, 0, 40, 50, 60, 0];

        Assert.False(IconPixels.HasAlpha(bgra));
    }

    [Fact]
    public void HasAlpha_TrueWhenAnyAlphaByteIsSet()
    {
        byte[] bgra = [10, 20, 30, 0, 40, 50, 60, 1];

        Assert.True(IconPixels.HasAlpha(bgra));
    }

    [Fact]
    public void ApplyMask_BlackMaskMakesPixelOpaque_WhiteMaskMakesItTransparent()
    {
        // Icon AND-mask semantics: black (0) = keep the pixel, white = transparent.
        byte[] color = [10, 20, 30, 0, 40, 50, 60, 0];
        byte[] mask = [0, 0, 0, 0, 255, 255, 255, 0];

        IconPixels.ApplyMask(color, mask);

        Assert.Equal(255, color[3]);
        Assert.Equal(0, color[7]);
        // Color channels stay untouched.
        Assert.Equal([10, 20, 30], color[..3]);
    }

    [Fact]
    public void MakeOpaque_SetsEveryAlphaByte()
    {
        byte[] bgra = [10, 20, 30, 0, 40, 50, 60, 0];

        IconPixels.MakeOpaque(bgra);

        Assert.Equal(255, bgra[3]);
        Assert.Equal(255, bgra[7]);
    }
}
