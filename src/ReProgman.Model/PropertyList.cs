using System.Buffers.Binary;
using System.Text;
using System.Xml;

namespace ReProgman.Model;

/// <summary>
/// Reads top-level string entries out of an Apple property list. Info.plist ships
/// in both the XML and the binary (bplist00) flavor, so both are handled; only
/// what the icon lookup needs is implemented, everything else reads as null.
/// </summary>
public static class PropertyList
{
    private static ReadOnlySpan<byte> BinaryMagic => "bplist00"u8;

    public static string? GetString(byte[] data, string key)
    {
        try
        {
            return data.AsSpan().StartsWith(BinaryMagic)
                ? ReadBinaryString(data, key)
                : ReadXmlString(data, key);
        }
        catch (Exception e) when (e is XmlException or FormatException or IndexOutOfRangeException
            or ArgumentOutOfRangeException or ArgumentException or OverflowException)
        {
            // A malformed plist is treated like a missing key: the caller falls
            // back to the conventional icon name.
            return null;
        }
    }

    // ------------------------------------------------------------------- XML

    private static string? ReadXmlString(byte[] data, string key)
    {
        using var stream = new MemoryStream(data, writable: false);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            // Info.plist declares Apple's DTD; ignoring it avoids a network fetch.
            DtdProcessing = DtdProcessing.Ignore,
            XmlResolver = null,
            IgnoreWhitespace = true,
            IgnoreComments = true,
        });

        var inRootDictionary = false;
        var matched = false;
        // Content is read with ReadElementContentAsString/Skip, which already leave
        // the reader on the next node, so the loop must not advance again.
        var positioned = false;
        while (positioned || reader.Read())
        {
            positioned = false;
            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            if (!inRootDictionary)
            {
                inRootDictionary = reader.Name == "dict" && !reader.IsEmptyElement;
                continue;
            }

            if (reader.Name == "key")
            {
                matched = reader.ReadElementContentAsString() == key;
                positioned = !reader.EOF;
                continue;
            }

            if (matched)
            {
                return reader.Name == "string" ? reader.ReadElementContentAsString() : null;
            }

            if (!reader.IsEmptyElement)
            {
                // Skipping whole values keeps nested dictionaries out of the way:
                // document type icons hide in those under the very same key name.
                reader.Skip();
                positioned = !reader.EOF;
            }
        }

        return null;
    }

    // ---------------------------------------------------------------- binary

    private static string? ReadBinaryString(byte[] data, string key)
    {
        if (data.Length < 40)
        {
            return null;
        }

        var trailer = data.AsSpan(data.Length - 32);
        int offsetSize = trailer[6];
        int refSize = trailer[7];
        var objectCount = (int)BinaryPrimitives.ReadUInt64BigEndian(trailer[8..]);
        var topObject = (int)BinaryPrimitives.ReadUInt64BigEndian(trailer[16..]);
        var offsetTable = (int)BinaryPrimitives.ReadUInt64BigEndian(trailer[24..]);

        if (offsetSize is < 1 or > 8 || refSize is < 1 or > 8 ||
            objectCount <= 0 || topObject >= objectCount ||
            offsetTable + (long)objectCount * offsetSize > data.Length)
        {
            return null;
        }

        var offsets = new int[objectCount];
        for (var i = 0; i < objectCount; i++)
        {
            offsets[i] = (int)ReadBigEndian(data, offsetTable + (i * offsetSize), offsetSize);
        }

        var root = offsets[topObject];
        if ((data[root] & 0xF0) != 0xD0)
        {
            return null;
        }

        var count = ReadElementCount(data, ref root, 0xD0);
        for (var i = 0; i < count; i++)
        {
            var keyRef = (int)ReadBigEndian(data, root + (i * refSize), refSize);
            if (keyRef >= objectCount || ReadString(data, offsets[keyRef]) != key)
            {
                continue;
            }

            var valueRef = (int)ReadBigEndian(data, root + ((count + i) * refSize), refSize);
            return valueRef < objectCount ? ReadString(data, offsets[valueRef]) : null;
        }

        return null;
    }

    /// <summary>
    /// Reads the low-nibble element count of a marker, following the 0xF escape to
    /// the integer object that carries larger counts. Advances to the payload.
    /// </summary>
    private static int ReadElementCount(byte[] data, ref int offset, byte expectedType)
    {
        var marker = data[offset++];
        if ((marker & 0xF0) != expectedType)
        {
            return -1;
        }

        var count = marker & 0x0F;
        if (count != 0x0F)
        {
            return count;
        }

        var sizeMarker = data[offset++];
        if ((sizeMarker & 0xF0) != 0x10)
        {
            return -1;
        }

        var byteCount = 1 << (sizeMarker & 0x0F);
        count = (int)ReadBigEndian(data, offset, byteCount);
        offset += byteCount;
        return count;
    }

    private static string? ReadString(byte[] data, int offset)
    {
        var type = (byte)(data[offset] & 0xF0);
        if (type is not (0x50 or 0x60))
        {
            return null;
        }

        var length = ReadElementCount(data, ref offset, type);
        if (length < 0)
        {
            return null;
        }

        return type == 0x50
            ? Encoding.ASCII.GetString(data, offset, length)
            : Encoding.BigEndianUnicode.GetString(data, offset, length * 2);
    }

    private static ulong ReadBigEndian(byte[] data, int offset, int size)
    {
        ulong value = 0;
        for (var i = 0; i < size; i++)
        {
            value = (value << 8) | data[offset + i];
        }

        return value;
    }
}
