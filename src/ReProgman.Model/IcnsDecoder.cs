using System.Buffers.Binary;
using System.Text;

namespace ReProgman.Model;

public enum IcnsImageFormat
{
    /// <summary>The chunk payload is a PNG file and can be handed to an image decoder as-is.</summary>
    Png,

    /// <summary>Unpremultiplied 32bpp BGRA pixels, top-down, matching the Windows icon path.</summary>
    Bgra32,
}

/// <summary>One icon representation taken out of an .icns file.</summary>
public sealed record IcnsImage(int Width, int Height, IcnsImageFormat Format, byte[] Data);

/// <summary>
/// Reads Apple icon suites (.icns). The file is a flat list of typed chunks whose
/// type also fixes the pixel size, so the right representation can be picked
/// without decoding anything. Modern chunks hold a PNG; the older ones hold
/// run-length encoded channel planes, which is what keeps 32x32 art available
/// for applications that predate the PNG chunks.
/// </summary>
public static class IcnsDecoder
{
    private const int HeaderSize = 8;

    private static ReadOnlySpan<byte> FileMagic => "icns"u8;

    private static ReadOnlySpan<byte> PngMagic => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static ReadOnlySpan<byte> ArgbMagic => "ARGB"u8;

    /// <summary>Chunk types whose payload is a complete image file (PNG or JPEG 2000).</summary>
    private static readonly Dictionary<string, int> ImageChunks = new(StringComparer.Ordinal)
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

    /// <summary>Legacy 24bpp chunk types and the mask chunk that carries their alpha.</summary>
    private static readonly Dictionary<string, (int Size, string Mask)> PlaneChunks = new(StringComparer.Ordinal)
    {
        ["is32"] = (16, "s8mk"),
        ["il32"] = (32, "l8mk"),
        ["ih32"] = (48, "h8mk"),
        ["it32"] = (128, "t8mk"),
    };

    /// <summary>Chunk types holding four run-length encoded ARGB planes.</summary>
    private static readonly Dictionary<string, int> ArgbChunks = new(StringComparer.Ordinal)
    {
        ["ic04"] = 16,
        ["ic05"] = 32,
    };

    public static IcnsImage? Decode(byte[] data, int preferredSize = 32)
    {
        var chunks = ReadChunks(data);
        if (chunks.Count == 0)
        {
            return null;
        }

        var candidates = new List<IcnsImage>();
        foreach (var (type, payload) in chunks)
        {
            if (ImageChunks.TryGetValue(type, out var imageSize))
            {
                // JPEG 2000 payloads (used by a few old bundles) have no decoder here.
                if (payload.AsSpan().StartsWith(PngMagic))
                {
                    candidates.Add(new IcnsImage(imageSize, imageSize, IcnsImageFormat.Png, payload));
                }

                continue;
            }

            if (PlaneChunks.TryGetValue(type, out var plane) &&
                DecodePlanes(type, payload, plane.Size, chunks.GetValueOrDefault(plane.Mask)) is { } legacy)
            {
                candidates.Add(legacy);
                continue;
            }

            if (ArgbChunks.TryGetValue(type, out var argbSize) && DecodeArgb(payload, argbSize) is { } argb)
            {
                candidates.Add(argb);
            }
        }

        return candidates
            .OrderBy(c => c.Width < preferredSize)
            .ThenBy(c => Math.Abs(c.Width - preferredSize))
            .ThenBy(c => c.Format != IcnsImageFormat.Png)
            .FirstOrDefault();
    }

    private static Dictionary<string, byte[]> ReadChunks(byte[] data)
    {
        var chunks = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        if (data.Length < HeaderSize * 2 || !data.AsSpan().StartsWith(FileMagic))
        {
            return chunks;
        }

        var end = Math.Min(data.Length, BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(4)));
        var offset = HeaderSize;
        while (offset + HeaderSize <= end)
        {
            var type = Encoding.ASCII.GetString(data, offset, 4);
            var length = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(offset + 4));
            if (length < HeaderSize || offset + length > end)
            {
                // A truncated chunk means the rest of the file cannot be trusted.
                break;
            }

            chunks[type] = data[(offset + HeaderSize)..(offset + length)];
            offset += length;
        }

        return chunks;
    }

    private static IcnsImage? DecodePlanes(string type, byte[] payload, int size, byte[]? mask)
    {
        var pixels = size * size;
        // The 128x128 variant prefixes its planes with four unused bytes.
        var body = type == "it32" && payload.Length > 4 ? payload.AsSpan(4) : payload;

        var channels = body.Length == pixels * 3 ? body.ToArray() : Unpack(body, pixels * 3);
        if (channels is null)
        {
            return null;
        }

        var bgra = new byte[pixels * 4];
        for (var i = 0; i < pixels; i++)
        {
            bgra[(i * 4) + 0] = channels[(pixels * 2) + i];
            bgra[(i * 4) + 1] = channels[pixels + i];
            bgra[(i * 4) + 2] = channels[i];
            bgra[(i * 4) + 3] = mask is not null && mask.Length >= pixels ? mask[i] : (byte)0xFF;
        }

        return new IcnsImage(size, size, IcnsImageFormat.Bgra32, bgra);
    }

    private static IcnsImage? DecodeArgb(byte[] payload, int size)
    {
        if (!payload.AsSpan().StartsWith(ArgbMagic))
        {
            return null;
        }

        var pixels = size * size;
        var channels = Unpack(payload.AsSpan(ArgbMagic.Length), pixels * 4);
        if (channels is null)
        {
            return null;
        }

        var bgra = new byte[pixels * 4];
        for (var i = 0; i < pixels; i++)
        {
            bgra[(i * 4) + 0] = channels[(pixels * 3) + i];
            bgra[(i * 4) + 1] = channels[(pixels * 2) + i];
            bgra[(i * 4) + 2] = channels[pixels + i];
            bgra[(i * 4) + 3] = channels[i];
        }

        return new IcnsImage(size, size, IcnsImageFormat.Bgra32, bgra);
    }

    /// <summary>
    /// Expands the icns run-length encoding: a lead byte below 0x80 introduces
    /// that many plus one literal bytes, otherwise it repeats the next byte
    /// (lead - 0x80 + 3) times. Returns null unless the expected size comes out
    /// exactly, which keeps a misidentified chunk from producing garbage pixels.
    /// </summary>
    private static byte[]? Unpack(ReadOnlySpan<byte> packed, int expectedLength)
    {
        var output = new byte[expectedLength];
        var written = 0;
        var offset = 0;
        while (offset < packed.Length && written < expectedLength)
        {
            var lead = packed[offset++];
            if (lead < 0x80)
            {
                var count = lead + 1;
                if (offset + count > packed.Length || written + count > expectedLength)
                {
                    return null;
                }

                packed.Slice(offset, count).CopyTo(output.AsSpan(written));
                offset += count;
                written += count;
                continue;
            }

            var repeat = lead - 0x80 + 3;
            if (offset >= packed.Length || written + repeat > expectedLength)
            {
                return null;
            }

            output.AsSpan(written, repeat).Fill(packed[offset++]);
            written += repeat;
        }

        return written == expectedLength ? output : null;
    }
}
