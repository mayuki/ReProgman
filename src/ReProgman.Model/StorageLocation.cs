namespace ReProgman.Model;

/// <summary>
/// Decides where ReProgman.ini and Groups.ini live. The application stays portable:
/// the files sit next to the executable — next to the .app bundle on macOS — and
/// only move to a per-user folder when that location cannot be written, which is
/// what App Translocation and read-only volumes produce.
/// </summary>
public static class StorageLocation
{
    private const string MacBundleExtension = ".app";

    /// <summary>
    /// Turns an executable directory into the folder that should hold the INI files.
    /// A macOS bundle keeps its executable in ReProgman.app/Contents/MacOS, so three
    /// levels are stripped to land beside the bundle instead of inside it.
    /// </summary>
    public static string GetPortableDirectory(string baseDirectory)
    {
        var directory = baseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!string.Equals(Path.GetFileName(directory), "MacOS", StringComparison.Ordinal))
        {
            return directory;
        }

        var contents = Path.GetDirectoryName(directory);
        if (contents is null || !string.Equals(Path.GetFileName(contents), "Contents", StringComparison.Ordinal))
        {
            return directory;
        }

        var bundle = Path.GetDirectoryName(contents);
        if (bundle is null || !bundle.EndsWith(MacBundleExtension, StringComparison.OrdinalIgnoreCase))
        {
            return directory;
        }

        return Path.GetDirectoryName(bundle) ?? directory;
    }

    /// <summary>
    /// Picks the portable directory when it can be written to, otherwise the
    /// fallback. A null fallback means "portable or nothing", the Windows behavior.
    /// </summary>
    public static string Resolve(string portableDirectory, string? fallbackDirectory, Func<string, bool> isWritable) =>
        fallbackDirectory is null || isWritable(portableDirectory) ? portableDirectory : fallbackDirectory;

    /// <summary>
    /// Probes a directory by actually creating and deleting a file in it. There is
    /// no reliable way to ask macOS whether a translocated bundle is writable.
    /// </summary>
    public static bool IsWritable(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var probe = Path.Combine(directory, ".reprogman-write-probe");
            using (var stream = new FileStream(probe, FileMode.Create, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose))
            {
                stream.WriteByte(0);
            }

            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }
}
