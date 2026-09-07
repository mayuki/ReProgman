using ReProgman.Model;

namespace ReProgman.Model.Tests;

public sealed class ApplicationBundleSourceTests : IDisposable
{
    private readonly string _root;

    public ApplicationBundleSourceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "ReProgmanBundleTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Bundle(params string[] segments)
    {
        var path = Path.Combine([_root, .. segments]);
        Directory.CreateDirectory(Path.Combine(path, "Contents", "MacOS"));
        return path;
    }

    [Fact]
    public void Enumerate_ReturnsBundleNamesWithoutTheAppExtension()
    {
        Bundle("Safari.app");

        var entries = new ApplicationBundleSource().Enumerate(_root, recursive: false).ToList();

        var entry = Assert.Single(entries);
        Assert.Equal("Safari", entry.Name);
        Assert.Equal(Path.Combine(_root, "Safari.app"), entry.Path);
    }

    [Fact]
    public void Enumerate_IgnoresPlainDirectoriesAndFiles()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Utilities"));
        File.WriteAllText(Path.Combine(_root, "readme.txt"), string.Empty);
        Bundle("Safari.app");

        var entries = new ApplicationBundleSource().Enumerate(_root, recursive: false).ToList();

        Assert.Equal(["Safari"], entries.Select(e => e.Name));
    }

    [Fact]
    public void Enumerate_DoesNotDescendIntoBundles()
    {
        // Helper tools nested inside a bundle are implementation details of the
        // host application and must not show up as separate program items.
        Bundle("Xcode.app", "Contents", "Applications", "Instruments.app");

        var entries = new ApplicationBundleSource().Enumerate(_root, recursive: true).ToList();

        Assert.Equal(["Xcode"], entries.Select(e => e.Name));
    }

    [Fact]
    public void Enumerate_SkipsHiddenBundles()
    {
        Bundle(".Hidden.app");
        Bundle("Safari.app");

        var entries = new ApplicationBundleSource().Enumerate(_root, recursive: false).ToList();

        Assert.Equal(["Safari"], entries.Select(e => e.Name));
    }

    [Fact]
    public void Enumerate_RecursiveFindsBundlesInNestedFolders()
    {
        Bundle("Adobe", "Extras", "Reader.app");

        var entries = new ApplicationBundleSource().Enumerate(_root, recursive: true).ToList();

        Assert.Equal(["Reader"], entries.Select(e => e.Name));
    }

    [Fact]
    public void Enumerate_NonRecursiveIgnoresNestedFolders()
    {
        Bundle("Adobe", "Reader.app");

        var entries = new ApplicationBundleSource().Enumerate(_root, recursive: false).ToList();

        Assert.Empty(entries);
    }
}
