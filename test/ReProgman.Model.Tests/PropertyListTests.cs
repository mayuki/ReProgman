using ReProgman.Model;

namespace ReProgman.Model.Tests;

public class PropertyListTests
{
    private const string Xml =
        """
        <?xml version="1.0" encoding="UTF-8"?>
        <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
        <plist version="1.0">
        <dict>
        	<key>CFBundleName</key>
        	<string>Test</string>
        	<key>CFBundleDocumentTypes</key>
        	<array>
        		<dict>
        			<key>CFBundleIconFile</key>
        			<string>DocumentIcon</string>
        		</dict>
        	</array>
        	<key>CFBundleIconFile</key>
        	<string>AppIcon</string>
        	<key>CFBundleDisplayName</key>
        	<string>日本語アプリ</string>
        </dict>
        </plist>
        """;

    // The same property list converted with `plutil -convert binary1`. It covers
    // the three string encodings a real Info.plist mixes: short ASCII, ASCII with
    // an extended length marker, and UTF-16.
    private const string BinaryBase64 =
        "YnBsaXN0MDDWAQIDBAUGBwgJCgsMXENGQnVuZGxlTmFtZV8QHkNGQnVuZGxlU2hvcnRWZXJzaW9uU3RyaW5nTG9uZ18QEENGQnVu" +
        "ZGxlSWNvbkZpbGVfEBNDRkJ1bmRsZURpc3BsYXlOYW1lXxAWTFNNaW5pbXVtU3lzdGVtVmVyc2lvbl8QFUNGQnVuZGxlRG9jdW1l" +
        "bnRUeXBlc1RUZXN0XxAzYSByYXRoZXIgbG9uZyB2YWx1ZSB0aGF0IGV4Y2VlZHMgZmlmdGVlbiBjaGFyYWN0ZXJzV0FwcEljb25m" +
        "ZeVnLIqeMKIw1zDqEAuhDdEDDlxEb2N1bWVudEljb24ACAAVACIAQwBWAGwAhQCdAKIA2ADgAO0A7wDxAPQAAAAAAAACAQAAAAAA" +
        "AAAPAAAAAAAAAAAAAAAAAAABAQ==";

    private static byte[] XmlBytes => System.Text.Encoding.UTF8.GetBytes(Xml);

    private static byte[] BinaryBytes => Convert.FromBase64String(BinaryBase64);

    [Fact]
    public void GetString_ReadsAKeyFromXml()
    {
        Assert.Equal("AppIcon", PropertyList.GetString(XmlBytes, "CFBundleIconFile"));
    }

    [Fact]
    public void GetString_ReadsAUnicodeValueFromXml()
    {
        Assert.Equal("日本語アプリ", PropertyList.GetString(XmlBytes, "CFBundleDisplayName"));
    }

    [Fact]
    public void GetString_IgnoresNestedDictionariesInXml()
    {
        // Document type icons live in a nested dict under the same key name and
        // must never win over the application icon.
        Assert.Equal("Test", PropertyList.GetString(XmlBytes, "CFBundleName"));
        Assert.NotEqual("DocumentIcon", PropertyList.GetString(XmlBytes, "CFBundleIconFile"));
    }

    [Fact]
    public void GetString_ReturnsNullForAMissingXmlKey()
    {
        Assert.Null(PropertyList.GetString(XmlBytes, "CFBundleExecutable"));
    }

    [Fact]
    public void GetString_ReadsAKeyFromBinary()
    {
        Assert.Equal("AppIcon", PropertyList.GetString(BinaryBytes, "CFBundleIconFile"));
    }

    [Fact]
    public void GetString_ReadsAUtf16ValueFromBinary()
    {
        Assert.Equal("日本語アプリ", PropertyList.GetString(BinaryBytes, "CFBundleDisplayName"));
    }

    [Fact]
    public void GetString_ReadsAnExtendedLengthValueFromBinary()
    {
        Assert.Equal(
            "a rather long value that exceeds fifteen characters",
            PropertyList.GetString(BinaryBytes, "CFBundleShortVersionStringLong"));
    }

    [Fact]
    public void GetString_ReturnsNullForAMissingBinaryKey()
    {
        Assert.Null(PropertyList.GetString(BinaryBytes, "CFBundleExecutable"));
    }

    [Fact]
    public void GetString_ReturnsNullForANonStringValue()
    {
        Assert.Null(PropertyList.GetString(BinaryBytes, "LSMinimumSystemVersion"));
    }

    [Fact]
    public void GetString_ReturnsNullForGarbage()
    {
        Assert.Null(PropertyList.GetString([1, 2, 3, 4], "CFBundleIconFile"));
        Assert.Null(PropertyList.GetString([], "CFBundleIconFile"));
    }
}
