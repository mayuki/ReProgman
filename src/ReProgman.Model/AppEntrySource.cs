namespace ReProgman.Model;

/// <summary>One launchable entry found while scanning a program folder.</summary>
public readonly record struct AppEntry(string Name, string Path);

/// <summary>
/// Supplies the platform-specific notion of "a launchable thing in a folder".
/// The grouping rules in <see cref="StartMenuScanner"/> stay shared: Windows
/// yields shortcut files, macOS yields application bundles.
/// </summary>
public interface IAppEntrySource
{
    IEnumerable<AppEntry> Enumerate(string directory, bool recursive);
}

/// <summary>Windows: .lnk and .url files below a Start Menu folder.</summary>
public sealed class ShortcutFileSource : IAppEntrySource
{
    private static readonly string[] ShortcutExtensions = [".lnk", ".url"];

    public IEnumerable<AppEntry> Enumerate(string directory, bool recursive)
    {
        var options = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        return new DirectoryInfo(directory)
            .EnumerateFiles("*", options)
            .Where(f => ShortcutExtensions.Contains(f.Extension, StringComparer.OrdinalIgnoreCase))
            .Where(f => !string.Equals(f.Name, "desktop.ini", StringComparison.OrdinalIgnoreCase))
            .Where(f => (f.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0)
            .Select(f => new AppEntry(Path.GetFileNameWithoutExtension(f.Name), f.FullName));
    }
}

/// <summary>
/// macOS: .app bundles below a folder such as /Applications. Bundles are
/// directories, so enumeration walks folders by hand and stops at every bundle —
/// the helper apps inside one belong to their host, not to the program group.
/// </summary>
public sealed class ApplicationBundleSource : IAppEntrySource
{
    private const string BundleExtension = ".app";

    public IEnumerable<AppEntry> Enumerate(string directory, bool recursive)
    {
        foreach (var child in new DirectoryInfo(directory).EnumerateDirectories())
        {
            // Dot-prefixed entries are hidden on macOS; .NET maps that to the
            // Hidden attribute, but the name check keeps the rule explicit.
            if (child.Name.StartsWith('.'))
            {
                continue;
            }

            if (child.Name.EndsWith(BundleExtension, StringComparison.OrdinalIgnoreCase))
            {
                yield return new AppEntry(child.Name[..^BundleExtension.Length], child.FullName);
                continue;
            }

            if (!recursive)
            {
                continue;
            }

            foreach (var nested in Enumerate(child.FullName, recursive: true))
            {
                yield return nested;
            }
        }
    }
}

/// <summary>Picks the entry source that matches the running operating system.</summary>
public static class AppEntrySources
{
    public static IAppEntrySource ForCurrentPlatform() =>
        OperatingSystem.IsWindows() ? new ShortcutFileSource() : new ApplicationBundleSource();
}
