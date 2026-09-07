namespace ReProgman.Model;

/// <summary>
/// Locates the parts of a macOS .app bundle that Program Manager needs. Only the
/// icon is read; the bundle is otherwise launched as-is.
/// </summary>
public static class MacAppBundle
{
    private const string IconExtension = ".icns";
    private const string ConventionalIconName = "AppIcon" + IconExtension;

    /// <summary>
    /// Returns the .icns file of an application bundle, or null when it has none
    /// that can be identified — the caller then keeps the generic group icon.
    /// </summary>
    public static string? FindIconFile(string bundlePath)
    {
        var resources = Path.Combine(bundlePath, "Contents", "Resources");
        if (!Directory.Exists(resources))
        {
            return null;
        }

        if (ReadIconName(bundlePath) is { Length: > 0 } name)
        {
            if (!name.EndsWith(IconExtension, StringComparison.OrdinalIgnoreCase))
            {
                name += IconExtension;
            }

            var named = Path.Combine(resources, name);
            if (File.Exists(named))
            {
                return named;
            }
        }

        var conventional = Path.Combine(resources, ConventionalIconName);
        if (File.Exists(conventional))
        {
            return conventional;
        }

        // A single .icns can only be the application icon. With several of them
        // the extras are document icons, and guessing would show the wrong art.
        var candidates = Directory.GetFiles(resources, "*" + IconExtension);
        return candidates.Length == 1 ? candidates[0] : null;
    }

    private static string? ReadIconName(string bundlePath)
    {
        var plist = Path.Combine(bundlePath, "Contents", "Info.plist");
        if (!File.Exists(plist))
        {
            return null;
        }

        try
        {
            var data = File.ReadAllBytes(plist);
            // CFBundleIconName is the asset catalog form; bundles that use it
            // usually still ship a matching .icns next to it.
            return PropertyList.GetString(data, "CFBundleIconFile")
                ?? PropertyList.GetString(data, "CFBundleIconName");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
