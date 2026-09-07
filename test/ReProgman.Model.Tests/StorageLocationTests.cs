using ReProgman.Model;

namespace ReProgman.Model.Tests;

public sealed class StorageLocationTests : IDisposable
{
    private readonly string _temp;

    public StorageLocationTests()
    {
        _temp = Path.Combine(Path.GetTempPath(), "ReProgmanStorage_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_temp);
    }

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    // Path.GetDirectoryName rewrites separators to the platform's own, so the
    // results are compared separator-insensitively to keep the tests green on
    // Windows as well as macOS.
    [Fact]
    public void GetPortableDirectory_StepsOutOfTheMacBundle()
    {
        var baseDirectory = Path.Combine("/Users/tomoyo/Apps", "ReProgman.app", "Contents", "MacOS") + "/";

        Assert.Equal("/Users/tomoyo/Apps", StorageLocation.GetPortableDirectory(baseDirectory).Replace('\\', '/'));
    }

    [Fact]
    public void GetPortableDirectory_WorksWithoutATrailingSeparator()
    {
        var baseDirectory = Path.Combine("/Applications", "ReProgman.app", "Contents", "MacOS");

        Assert.Equal("/Applications", StorageLocation.GetPortableDirectory(baseDirectory).Replace('\\', '/'));
    }

    [Fact]
    public void GetPortableDirectory_KeepsAPlainExecutableDirectory()
    {
        var baseDirectory = Path.Combine(_temp, "bin");

        Assert.Equal(baseDirectory, StorageLocation.GetPortableDirectory(baseDirectory + Path.DirectorySeparatorChar));
    }

    [Fact]
    public void GetPortableDirectory_OnlyStripsTheExecutableFolderOfABundle()
    {
        // Contents/Resources is not where the executable lives, so nothing is stripped.
        var baseDirectory = Path.Combine("/Applications", "ReProgman.app", "Contents", "Resources");

        Assert.Equal(baseDirectory, StorageLocation.GetPortableDirectory(baseDirectory));
    }

    [Fact]
    public void Resolve_PrefersThePortableDirectory()
    {
        Assert.Equal("/portable", StorageLocation.Resolve("/portable", "/fallback", _ => true));
    }

    [Fact]
    public void Resolve_FallsBackWhenThePortableDirectoryIsNotWritable()
    {
        Assert.Equal("/fallback", StorageLocation.Resolve("/portable", "/fallback", _ => false));
    }

    [Fact]
    public void Resolve_KeepsThePortableDirectoryWhenThereIsNoFallback()
    {
        // Windows stays portable-only: an unwritable install folder silently
        // loses settings, exactly like the original PROGMAN.INI behavior.
        Assert.Equal("/portable", StorageLocation.Resolve("/portable", null, _ => false));
    }

    [Fact]
    public void IsWritable_IsTrueForAWritableDirectory()
    {
        Assert.True(StorageLocation.IsWritable(_temp));
    }

    [Fact]
    public void IsWritable_CreatesAMissingDirectory()
    {
        var missing = Path.Combine(_temp, "created");

        Assert.True(StorageLocation.IsWritable(missing));
        Assert.True(Directory.Exists(missing));
    }

    [Fact]
    public void IsWritable_IsFalseWhenTheDirectoryCannotExist()
    {
        var file = Path.Combine(_temp, "blocker");
        File.WriteAllText(file, string.Empty);

        Assert.False(StorageLocation.IsWritable(Path.Combine(file, "child")));
    }

    [Fact]
    public void IsWritable_LeavesNoProbeFileBehind()
    {
        Assert.True(StorageLocation.IsWritable(_temp));
        Assert.Empty(Directory.EnumerateFileSystemEntries(_temp));
    }
}
