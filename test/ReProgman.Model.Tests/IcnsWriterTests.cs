using ReProgman.Model;

namespace ReProgman.Model.Tests;

public sealed class IcnsWriterTests
{
    private static byte[] FakePng(byte seed) =>
        [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A, seed];

    private static string ReadType(byte[] data, int offset) =>
        System.Text.Encoding.ASCII.GetString(data, offset, 4);

    private static int ReadInt32BigEndian(byte[] data, int offset) =>
        (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];

    [Fact]
    public void Build_WritesTheFileHeader()
    {
        var icns = IcnsWriter.Build([new IcnsPngImage("ic07", FakePng(1))]);

        Assert.Equal("icns", ReadType(icns, 0));
        // The length in the header covers the whole file, header included.
        Assert.Equal(icns.Length, ReadInt32BigEndian(icns, 4));
    }

    [Fact]
    public void Build_WritesEachImageAsATypedChunk()
    {
        var png = FakePng(7);

        var icns = IcnsWriter.Build([new IcnsPngImage("ic08", png)]);

        Assert.Equal("ic08", ReadType(icns, 8));
        Assert.Equal(8 + png.Length, ReadInt32BigEndian(icns, 12));
        Assert.Equal(png, icns[16..]);
    }

    [Fact]
    public void Build_KeepsTheImagesInOrder()
    {
        var first = FakePng(1);
        var second = FakePng(2);

        var icns = IcnsWriter.Build([new IcnsPngImage("icp4", first), new IcnsPngImage("ic10", second)]);

        Assert.Equal("icp4", ReadType(icns, 8));
        Assert.Equal("ic10", ReadType(icns, 8 + 8 + first.Length));
        Assert.Equal(8 + (8 + first.Length) + (8 + second.Length), icns.Length);
    }

    [Fact]
    public void Build_RejectsAnEmptyIcon()
    {
        Assert.Throws<ArgumentException>(() => IcnsWriter.Build([]));
    }

    [Fact]
    public void Build_RejectsATypeTheFormatDoesNotCarryPngIn()
    {
        // is32 exists, but it holds run-length encoded planes rather than a PNG.
        Assert.Throws<ArgumentException>(() => new IcnsPngImage("is32", FakePng(1)));
        Assert.Throws<ArgumentException>(() => new IcnsPngImage("nope!", FakePng(1)));
    }

    [Fact]
    public void Build_RejectsAPayloadThatIsNotAPng()
    {
        Assert.Throws<ArgumentException>(() => new IcnsPngImage("ic07", [1, 2, 3]));
    }

    [Fact]
    public void Build_ProducesAFileTheDecoderReadsBack()
    {
        // The decoder is the other half of the format contract, so a round trip
        // is the cheapest check that the chunk framing is right.
        var png = FakePng(9);

        var icns = IcnsWriter.Build([new IcnsPngImage("icp5", png), new IcnsPngImage("ic07", png)]);

        var image = IcnsDecoder.Decode(icns, preferredSize: 32);
        Assert.NotNull(image);
        Assert.Equal(32, image!.Width);
        Assert.Equal(IcnsImageFormat.Png, image.Format);
        Assert.Equal(png, image.Data);
    }
}
