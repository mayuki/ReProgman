using System.Buffers.Binary;
using System.Text;

namespace ReProgman.Model;

/// <summary>
/// One PNG representation to put into an .icns file. The chunk type is what fixes
/// the pixel size, so it is the caller's choice of type that decides which slot
/// of the icon suite the image fills.
/// </summary>
public sealed class IcnsPngImage
{
    /// <summary>
    /// The chunk types that carry a plain PNG payload, with the pixel size each
    /// one stands for. The @2x types are the retina companions: `ic11` is the
    /// 16x16@2x slot, `ic12` the 32x32@2x one, and so on.
    /// </summary>
    private static readonly Dictionary<string, int> PngChunks = new(StringComparer.Ordinal)
    {
        ["icp4"] = 16,
        ["icp5"] = 32,
        ["icp6"] = 64,
        ["ic07"] = 128,
        ["ic08"] = 256,
        ["ic09"] = 512,
        ["ic10"] = 1024,
        ["ic11"] = 32,
        ["ic12"] = 64,
        ["ic13"] = 256,
        ["ic14"] = 512,
    };

    private static ReadOnlySpan<byte> PngMagic => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public IcnsPngImage(string type, byte[] png)
    {
        if (!PngChunks.ContainsKey(type))
        {
            throw new ArgumentException($"'{type}' is not a chunk type that carries a PNG.", nameof(type));
        }

        if (png.Length < PngMagic.Length || !png.AsSpan(0, PngMagic.Length).SequenceEqual(PngMagic))
        {
            throw new ArgumentException("The payload is not a PNG.", nameof(png));
        }

        Type = type;
        Png = png;
    }

    public string Type { get; }

    public byte[] Png { get; }

    /// <summary>The pixel size the chunk type stands for.</summary>
    public int Size => PngChunks[Type];
}

/// <summary>
/// Writes Apple icon suites (.icns): the file magic and length, then one typed
/// chunk per representation. Written by hand, like <see cref="IcoWriter"/>, so the
/// icon can be produced on any platform instead of only where `iconutil` lives.
/// </summary>
public static class IcnsWriter
{
    private const int HeaderSize = 8;

    private static ReadOnlySpan<byte> FileMagic => "icns"u8;

    public static byte[] Build(IReadOnlyList<IcnsPngImage> images)
    {
        if (images.Count == 0)
        {
            throw new ArgumentException("An icon suite needs at least one image.", nameof(images));
        }

        var length = HeaderSize + images.Sum(i => HeaderSize + i.Png.Length);
        var data = new byte[length];

        FileMagic.CopyTo(data);
        // Both the file and every chunk count their own header in the length.
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(4), length);

        var offset = HeaderSize;
        foreach (var image in images)
        {
            Encoding.ASCII.GetBytes(image.Type, data.AsSpan(offset));
            BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(offset + 4), HeaderSize + image.Png.Length);
            image.Png.CopyTo(data, offset + HeaderSize);
            offset += HeaderSize + image.Png.Length;
        }

        return data;
    }
}
