using ReProgman.Model;

namespace ReProgman.Model.Tests;

public sealed class IcoWriterTests
{
    private static byte[] Bgra(int size, byte seed = 0)
    {
        var pixels = new byte[size * size * 4];
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = (byte)(seed + i);
        }

        return pixels;
    }

    private static ushort ReadUInt16(byte[] data, int offset) => (ushort)(data[offset] | (data[offset + 1] << 8));

    private static int ReadInt32(byte[] data, int offset) =>
        data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24);

    [Fact]
    public void Build_WritesAnIconDirectoryHeader()
    {
        var ico = IcoWriter.Build([IcoImage.FromBgra(16, Bgra(16)), IcoImage.FromBgra(32, Bgra(32))]);

        Assert.Equal(0, ReadUInt16(ico, 0));
        Assert.Equal(1, ReadUInt16(ico, 2));
        Assert.Equal(2, ReadUInt16(ico, 4));
    }

    [Fact]
    public void Build_DescribesEachImageInTheDirectory()
    {
        var small = IcoImage.FromBgra(16, Bgra(16));

        var ico = IcoWriter.Build([small]);

        Assert.Equal(16, ico[6]);
        Assert.Equal(16, ico[7]);
        Assert.Equal(0, ico[8]);
        Assert.Equal(0, ico[9]);
        Assert.Equal(1, ReadUInt16(ico, 10));
        Assert.Equal(32, ReadUInt16(ico, 12));
        Assert.Equal(small.Data.Length, ReadInt32(ico, 14));
    }

    [Fact]
    public void Build_Records256AsZeroBecauseTheFieldIsOneByte()
    {
        var ico = IcoWriter.Build([IcoImage.FromPng(256, [1, 2, 3])]);

        Assert.Equal(0, ico[6]);
        Assert.Equal(0, ico[7]);
    }

    [Fact]
    public void Build_PlacesImagesAfterTheDirectoryInOrder()
    {
        var first = IcoImage.FromPng(16, [1, 2, 3]);
        var second = IcoImage.FromPng(32, [4, 5]);

        var ico = IcoWriter.Build([first, second]);

        var directoryLength = 6 + 16 * 2;
        Assert.Equal(directoryLength, ReadInt32(ico, 18));
        Assert.Equal(directoryLength + first.Data.Length, ReadInt32(ico, 18 + 16));
        Assert.Equal(directoryLength + first.Data.Length + second.Data.Length, ico.Length);
        Assert.Equal(first.Data, ico[directoryLength..(directoryLength + first.Data.Length)]);
        Assert.Equal(second.Data, ico[(directoryLength + first.Data.Length)..]);
    }

    [Fact]
    public void Build_RejectsAnEmptyIcon()
    {
        Assert.Throws<ArgumentException>(() => IcoWriter.Build([]));
    }

    [Fact]
    public void FromPng_StoresTheBytesUnchanged()
    {
        byte[] png = [0x89, (byte)'P', (byte)'N', (byte)'G'];

        Assert.Equal(png, IcoImage.FromPng(64, png).Data);
    }

    [Fact]
    public void FromBgra_WritesADibHeaderWithTheDoubledHeight()
    {
        var image = IcoImage.FromBgra(16, Bgra(16));

        Assert.Equal(40, ReadInt32(image.Data, 0));
        Assert.Equal(16, ReadInt32(image.Data, 4));
        // The height covers the color rows plus the mask rows, as the format requires.
        Assert.Equal(32, ReadInt32(image.Data, 8));
        Assert.Equal(1, ReadUInt16(image.Data, 12));
        Assert.Equal(32, ReadUInt16(image.Data, 14));
        Assert.Equal(0, ReadInt32(image.Data, 16));
    }

    [Fact]
    public void FromBgra_AppendsAnAllOpaqueMaskPaddedToFourBytes()
    {
        var image = IcoImage.FromBgra(16, Bgra(16));

        // 16 mask bits per row round up to one 4 byte group.
        var maskLength = 4 * 16;
        Assert.Equal(40 + 16 * 16 * 4 + maskLength, image.Data.Length);
        Assert.All(image.Data[^maskLength..], b => Assert.Equal(0, b));
    }

    [Fact]
    public void FromBgra_FlipsTheRowsBottomUp()
    {
        var pixels = new byte[2 * 2 * 4];
        pixels[0] = 0xAA; // top-left
        pixels[8] = 0xBB; // bottom-left

        var image = IcoImage.FromBgra(2, pixels);

        var colors = image.Data.AsSpan(40);
        Assert.Equal(0xBB, colors[0]);
        Assert.Equal(0xAA, colors[8]);
    }

    [Fact]
    public void FromBgra_RejectsPixelsThatDoNotMatchTheSize()
    {
        Assert.Throws<ArgumentException>(() => IcoImage.FromBgra(16, Bgra(8)));
    }

    [Fact]
    public void FromBgra_RejectsSizesTheFormatCannotDescribe()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => IcoImage.FromBgra(257, Bgra(257)));
        Assert.Throws<ArgumentOutOfRangeException>(() => IcoImage.FromBgra(0, []));
    }
}
