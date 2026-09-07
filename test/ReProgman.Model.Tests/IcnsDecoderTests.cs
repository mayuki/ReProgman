using System.Buffers.Binary;
using System.Text;
using ReProgman.Model;

namespace ReProgman.Model.Tests;

public class IcnsDecoderTests
{
    private static readonly byte[] PngMagic = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>Builds an .icns file from raw (type, payload) chunks.</summary>
    private static byte[] Icns(params (string Type, byte[] Payload)[] chunks)
    {
        var body = new List<byte>();
        foreach (var (type, payload) in chunks)
        {
            body.AddRange(Encoding.ASCII.GetBytes(type));
            body.AddRange(BigEndian(payload.Length + 8));
            body.AddRange(payload);
        }

        var file = new List<byte>();
        file.AddRange("icns"u8);
        file.AddRange(BigEndian(body.Count + 8));
        file.AddRange(body);
        return [.. file];
    }

    private static byte[] BigEndian(int value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(bytes, value);
        return bytes;
    }

    private static byte[] Png(params byte[] tail) => [.. PngMagic, .. tail];

    /// <summary>Packs a channel plane the way the legacy icns run-length encoding does.</summary>
    private static byte[] Pack(byte[] plane)
    {
        var packed = new List<byte>();
        var index = 0;
        while (index < plane.Length)
        {
            var run = 1;
            while (run < 130 && index + run < plane.Length && plane[index + run] == plane[index])
            {
                run++;
            }

            if (run >= 3)
            {
                packed.Add((byte)(0x80 + run - 3));
                packed.Add(plane[index]);
                index += run;
                continue;
            }

            var literal = new List<byte>();
            while (index < plane.Length && literal.Count < 128)
            {
                var repeats = 1;
                while (repeats < 3 && index + repeats < plane.Length && plane[index + repeats] == plane[index])
                {
                    repeats++;
                }

                if (repeats >= 3)
                {
                    break;
                }

                literal.Add(plane[index++]);
            }

            packed.Add((byte)(literal.Count - 1));
            packed.AddRange(literal);
        }

        return [.. packed];
    }

    private static byte[] Fill(int count, byte value) => Enumerable.Repeat(value, count).ToArray();

    [Fact]
    public void Decode_ReturnsNullForSomethingThatIsNotAnIcns()
    {
        Assert.Null(IcnsDecoder.Decode([1, 2, 3, 4, 5, 6, 7, 8]));
        Assert.Null(IcnsDecoder.Decode([]));
    }

    [Fact]
    public void Decode_ReturnsNullWhenNoChunkCanBeRead()
    {
        // JPEG 2000 payloads cannot be rendered, so a file that only has those
        // leaves the caller with the hand-drawn group icon.
        var icns = Icns(("ic09", [0, 0, 0, 0x0C, .. "jP  "u8]), ("TOC ", [1, 2, 3, 4]));

        Assert.Null(IcnsDecoder.Decode(icns));
    }

    [Fact]
    public void Decode_PrefersTheRequestedSize()
    {
        var icns = Icns(("ic07", Png(1)), ("ic11", Png(2)), ("ic13", Png(3)));

        var image = IcnsDecoder.Decode(icns, preferredSize: 32);

        Assert.NotNull(image);
        Assert.Equal(32, image.Width);
        Assert.Equal(IcnsImageFormat.Png, image.Format);
        Assert.Equal(Png(2), image.Data);
    }

    [Fact]
    public void Decode_FallsBackToTheSmallestLargerImage()
    {
        var icns = Icns(("ic09", Png(1)), ("ic07", Png(2)));

        var image = IcnsDecoder.Decode(icns, preferredSize: 32);

        Assert.NotNull(image);
        Assert.Equal(128, image.Width);
        Assert.Equal(Png(2), image.Data);
    }

    [Fact]
    public void Decode_FallsBackToTheLargestSmallerImage()
    {
        var icns = Icns(("icp4", Png(1)));

        var image = IcnsDecoder.Decode(icns, preferredSize: 32);

        Assert.NotNull(image);
        Assert.Equal(16, image.Width);
    }

    [Fact]
    public void Decode_SkipsChunksThatAreNotIcons()
    {
        var icns = Icns(("TOC ", [1, 2, 3, 4]), ("info", "bplist00"u8.ToArray()), ("ic11", Png(7)));

        var image = IcnsDecoder.Decode(icns, preferredSize: 32);

        Assert.NotNull(image);
        Assert.Equal(Png(7), image.Data);
    }

    [Fact]
    public void Decode_IgnoresATruncatedChunk()
    {
        var icns = Icns(("ic11", Png(9)));
        var truncated = icns[..^3];

        var image = IcnsDecoder.Decode(truncated, preferredSize: 32);

        Assert.Null(image);
    }

    [Fact]
    public void Decode_ReadsAnUncompressedLegacyIcon()
    {
        // il32 is three 32x32 channel planes; the matching l8mk carries the alpha.
        byte[] planes = [.. Fill(1024, 0x10), .. Fill(1024, 0x20), .. Fill(1024, 0x30)];

        var image = IcnsDecoder.Decode(
            Icns(("il32", planes), ("l8mk", Fill(1024, 0x80))),
            preferredSize: 32);

        Assert.NotNull(image);
        Assert.Equal(IcnsImageFormat.Bgra32, image.Format);
        Assert.Equal(32, image.Width);
        Assert.Equal(32 * 32 * 4, image.Data.Length);
        Assert.Equal([0x30, 0x20, 0x10, 0x80], image.Data[..4]);
    }

    [Fact]
    public void Decode_UnpacksRunLengthEncodedLegacyIcons()
    {
        var red = Fill(1024, 0xFF);
        red[0] = 0x01;
        red[1] = 0x02;
        byte[] packed = [.. Pack(red), .. Pack(Fill(1024, 0x00)), .. Pack(Fill(1024, 0x40))];

        var image = IcnsDecoder.Decode(
            Icns(("il32", packed), ("l8mk", Fill(1024, 0xFF))),
            preferredSize: 32);

        Assert.NotNull(image);
        Assert.Equal([0x40, 0x00, 0x01, 0xFF], image.Data[..4]);
        Assert.Equal([0x40, 0x00, 0x02, 0xFF], image.Data[4..8]);
        Assert.Equal([0x40, 0x00, 0xFF, 0xFF], image.Data[8..12]);
    }

    [Fact]
    public void Decode_MakesALegacyIconOpaqueWithoutItsMask()
    {
        byte[] planes = [.. Fill(1024, 0x10), .. Fill(1024, 0x20), .. Fill(1024, 0x30)];

        var image = IcnsDecoder.Decode(Icns(("il32", planes)), preferredSize: 32);

        Assert.NotNull(image);
        Assert.Equal([0x30, 0x20, 0x10, 0xFF], image.Data[..4]);
    }

    [Fact]
    public void Decode_ReadsTheArgbForm()
    {
        byte[] argb =
        [
            .. "ARGB"u8,
            .. Pack(Fill(1024, 0x80)),
            .. Pack(Fill(1024, 0x11)),
            .. Pack(Fill(1024, 0x22)),
            .. Pack(Fill(1024, 0x33)),
        ];

        var image = IcnsDecoder.Decode(Icns(("ic05", argb)), preferredSize: 32);

        Assert.NotNull(image);
        Assert.Equal(32, image.Width);
        Assert.Equal([0x33, 0x22, 0x11, 0x80], image.Data[..4]);
    }

    [Fact]
    public void Decode_SkipsTheHeaderOfThe128PixelLegacyIcon()
    {
        // it32 payloads start with four zero bytes before the packed planes.
        byte[] payload =
        [
            0, 0, 0, 0,
            .. Pack(Fill(128 * 128, 0x10)),
            .. Pack(Fill(128 * 128, 0x20)),
            .. Pack(Fill(128 * 128, 0x30)),
        ];

        var image = IcnsDecoder.Decode(Icns(("it32", payload)), preferredSize: 32);

        Assert.NotNull(image);
        Assert.Equal(128, image.Width);
        Assert.Equal([0x30, 0x20, 0x10, 0xFF], image.Data[..4]);
    }

    [Fact]
    public void Decode_PrefersPngOverAnEquallySizedLegacyIcon()
    {
        byte[] planes = [.. Fill(1024, 0x10), .. Fill(1024, 0x20), .. Fill(1024, 0x30)];

        var image = IcnsDecoder.Decode(Icns(("il32", planes), ("ic11", Png(5))), preferredSize: 32);

        Assert.NotNull(image);
        Assert.Equal(IcnsImageFormat.Png, image.Format);
    }
}
