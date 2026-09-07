namespace ReProgman.Model;

/// <summary>
/// One image inside a Windows .ico file, already encoded the way the format
/// stores it: either a bottom-up 32bpp DIB or a PNG stream.
/// </summary>
public sealed class IcoImage
{
    private IcoImage(int size, byte[] data)
    {
        Size = size;
        Data = data;
    }

    /// <summary>Edge length in pixels; icons are always square.</summary>
    public int Size { get; }

    /// <summary>The payload exactly as it appears in the file.</summary>
    public byte[] Data { get; }

    /// <summary>
    /// Encodes top-down BGRA pixels as the DIB variant: a BITMAPINFOHEADER whose
    /// height covers the color rows plus the mask rows, bottom-up color data, and
    /// an all-zero AND mask (the alpha channel does the masking on its own, but
    /// the mask has to be there for the file to be well formed).
    /// </summary>
    public static IcoImage FromBgra(int size, byte[] topDownBgra)
    {
        ValidateSize(size);
        var stride = size * 4;
        if (topDownBgra.Length != stride * size)
        {
            throw new ArgumentException($"Expected {stride * size} bytes of BGRA for {size}x{size}, got {topDownBgra.Length}.", nameof(topDownBgra));
        }

        var maskStride = (size + 31) / 32 * 4;
        var data = new byte[40 + stride * size + maskStride * size];
        var writer = new SpanWriter(data);

        writer.Int32(40);           // biSize
        writer.Int32(size);         // biWidth
        writer.Int32(size * 2);     // biHeight: color rows + mask rows
        writer.UInt16(1);           // biPlanes
        writer.UInt16(32);          // biBitCount
        writer.Int32(0);            // biCompression: BI_RGB
        writer.Int32(stride * size);// biSizeImage
        writer.Int32(0);            // biXPelsPerMeter
        writer.Int32(0);            // biYPelsPerMeter
        writer.Int32(0);            // biClrUsed
        writer.Int32(0);            // biClrImportant

        for (var row = size - 1; row >= 0; row--)
        {
            writer.Bytes(topDownBgra.AsSpan(row * stride, stride));
        }

        // The mask rows stay zero, which means "show every pixel".
        return new IcoImage(size, data);
    }

    /// <summary>Stores a PNG stream as-is, the usual encoding for the 256px entry.</summary>
    public static IcoImage FromPng(int size, byte[] png)
    {
        ValidateSize(size);
        return new IcoImage(size, png);
    }

    private static void ValidateSize(int size)
    {
        if (size is < 1 or > 256)
        {
            throw new ArgumentOutOfRangeException(nameof(size), size, "An icon image must be between 1 and 256 pixels.");
        }
    }
}

/// <summary>
/// Assembles a Windows .ico file. Written by hand so the icon can be produced
/// from the app's own drawing without dragging in an imaging library, and so it
/// keeps working under NativeAOT.
/// </summary>
public static class IcoWriter
{
    private const int DirectoryEntryLength = 16;

    public static byte[] Build(IReadOnlyList<IcoImage> images)
    {
        if (images.Count == 0)
        {
            throw new ArgumentException("An icon needs at least one image.", nameof(images));
        }

        var directoryLength = 6 + DirectoryEntryLength * images.Count;
        var data = new byte[directoryLength + images.Sum(i => i.Data.Length)];
        var writer = new SpanWriter(data);

        writer.UInt16(0); // reserved
        writer.UInt16(1); // type: icon
        writer.UInt16((ushort)images.Count);

        var offset = directoryLength;
        foreach (var image in images)
        {
            // 256 does not fit in a byte and is written as 0 by convention.
            writer.Byte((byte)(image.Size == 256 ? 0 : image.Size));
            writer.Byte((byte)(image.Size == 256 ? 0 : image.Size));
            writer.Byte(0); // palette entries: none, the images are true color
            writer.Byte(0); // reserved
            writer.UInt16(1);  // color planes
            writer.UInt16(32); // bits per pixel
            writer.Int32(image.Data.Length);
            writer.Int32(offset);
            offset += image.Data.Length;
        }

        foreach (var image in images)
        {
            writer.Bytes(image.Data);
        }

        return data;
    }
}

/// <summary>Little-endian sequential writer over a byte buffer.</summary>
internal ref struct SpanWriter(Span<byte> buffer)
{
    private readonly Span<byte> _buffer = buffer;
    private int _position = 0;

    public void Byte(byte value) => _buffer[_position++] = value;

    public void UInt16(ushort value)
    {
        Byte((byte)value);
        Byte((byte)(value >> 8));
    }

    public void Int32(int value)
    {
        Byte((byte)value);
        Byte((byte)(value >> 8));
        Byte((byte)(value >> 16));
        Byte((byte)(value >> 24));
    }

    public void Bytes(ReadOnlySpan<byte> value)
    {
        value.CopyTo(_buffer[_position..]);
        _position += value.Length;
    }
}
