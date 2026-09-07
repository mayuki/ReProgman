using ReProgman.Model;

namespace ReProgman.Model.Tests;

/// <summary>
/// The grouping rules are shared with the Windows scanner; these cover them with
/// the macOS entry source so that /Applications maps onto Program Manager groups.
/// </summary>
public sealed class BundleScannerTests : IDisposable
{
    private readonly string _root;

    public BundleScannerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "ReProgmanBundleScan_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private void Bundle(params string[] segments) =>
        Directory.CreateDirectory(Path.Combine([_root, .. segments, "Contents", "MacOS"]));

    [Fact]
    public void Scan_SubfoldersBecomeGroupsAndLooseBundlesGoToMain()
    {
        Bundle("Safari.app");
        Bundle("Utilities", "Terminal.app");

        var groups = new StartMenuScanner(new ApplicationBundleSource()).Scan([_root]);

        Assert.Equal(["Main", "Utilities"], groups.Select(g => g.Name));
        Assert.Equal("Safari", Assert.Single(groups[0].Items).Name);
        Assert.Equal("Terminal", Assert.Single(groups[1].Items).Name);
    }

    [Fact]
    public void Scan_ItemPathIsTheBundleItself()
    {
        Bundle("Utilities", "Terminal.app");

        var groups = new StartMenuScanner(new ApplicationBundleSource()).Scan([_root]);

        Assert.Equal(
            Path.Combine(_root, "Utilities", "Terminal.app"),
            Assert.Single(Assert.Single(groups).Items).Path);
    }
}
