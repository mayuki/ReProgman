using ReProgman.Model;

namespace ReProgman.Model.Tests;

public sealed class MacAppBundleTests : IDisposable
{
    private readonly string _root;

    public MacAppBundleTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "ReProgmanBundle_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string CreateBundle(string? iconValue, params string[] resources)
    {
        var bundle = Path.Combine(_root, "Test.app");
        var resourcesDirectory = Path.Combine(bundle, "Contents", "Resources");
        Directory.CreateDirectory(resourcesDirectory);
        var icon = iconValue is null
            ? string.Empty
            : $"<key>CFBundleIconFile</key><string>{iconValue}</string>";
        File.WriteAllText(
            Path.Combine(bundle, "Contents", "Info.plist"),
            $"""<?xml version="1.0" encoding="UTF-8"?><plist version="1.0"><dict>{icon}</dict></plist>""");
        foreach (var resource in resources)
        {
            File.WriteAllBytes(Path.Combine(resourcesDirectory, resource), []);
        }

        return bundle;
    }

    [Fact]
    public void FindIconFile_UsesCFBundleIconFile()
    {
        var bundle = CreateBundle("Custom.icns", "Custom.icns", "AppIcon.icns");

        Assert.Equal(
            Path.Combine(bundle, "Contents", "Resources", "Custom.icns"),
            MacAppBundle.FindIconFile(bundle));
    }

    [Fact]
    public void FindIconFile_AddsTheMissingIcnsExtension()
    {
        // Info.plist commonly stores the icon name without its extension.
        var bundle = CreateBundle("Custom", "Custom.icns");

        Assert.Equal(
            Path.Combine(bundle, "Contents", "Resources", "Custom.icns"),
            MacAppBundle.FindIconFile(bundle));
    }

    [Fact]
    public void FindIconFile_FallsBackToTheConventionalName()
    {
        var bundle = CreateBundle(iconValue: null, "AppIcon.icns");

        Assert.Equal(
            Path.Combine(bundle, "Contents", "Resources", "AppIcon.icns"),
            MacAppBundle.FindIconFile(bundle));
    }

    [Fact]
    public void FindIconFile_FallsBackToTheOnlyIconInTheBundle()
    {
        var bundle = CreateBundle(iconValue: null, "Whatever.icns");

        Assert.Equal(
            Path.Combine(bundle, "Contents", "Resources", "Whatever.icns"),
            MacAppBundle.FindIconFile(bundle));
    }

    [Fact]
    public void FindIconFile_ReturnsNullWhenTheNamedIconIsMissingAndThereAreSeveral()
    {
        // Guessing between unrelated document icons would be worse than the
        // hand-drawn group icon the caller falls back to.
        var bundle = CreateBundle("Gone.icns", "DocA.icns", "DocB.icns");

        Assert.Null(MacAppBundle.FindIconFile(bundle));
    }

    [Fact]
    public void FindIconFile_ReturnsNullForSomethingThatIsNotABundle()
    {
        Assert.Null(MacAppBundle.FindIconFile(_root));
    }
}
