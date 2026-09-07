using System.Diagnostics;
using System.Runtime.Versioning;

namespace ReProgman.Services;

public static class ShellLauncher
{
    private const string BundleExtension = ".app";
    private const string OpenTool = "/usr/bin/open";

    /// <summary>Launches a shortcut or application the way a double click would.</summary>
    public static void Launch(string path, bool minimized = false)
    {
        if (OperatingSystem.IsMacOS())
        {
            StartOnMac(path, arguments: null);
            return;
        }

        Process.Start(new ProcessStartInfo(path)
        {
            UseShellExecute = true,
            WindowStyle = minimized ? ProcessWindowStyle.Minimized : ProcessWindowStyle.Normal,
        })?.Dispose();
    }

    /// <summary>
    /// Launches a Run-dialog command line. The whole text is tried as a single path first
    /// so that unquoted paths with spaces keep working; otherwise it is split into
    /// program + arguments at the first space.
    /// </summary>
    public static void LaunchCommandLine(string commandLine, bool minimized)
    {
        var text = commandLine.Trim();
        if (text.Length == 0)
        {
            return;
        }

        var (fileName, arguments) = SplitCommandLine(text);
        if (OperatingSystem.IsMacOS())
        {
            // Run Minimized has no macOS equivalent, so the checkbox is ignored here.
            StartOnMac(fileName, arguments);
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = true,
            WindowStyle = minimized ? ProcessWindowStyle.Minimized : ProcessWindowStyle.Normal,
        })?.Dispose();
    }

    private static (string FileName, string Arguments) SplitCommandLine(string text)
    {
        if (text.StartsWith('"'))
        {
            var closing = text.IndexOf('"', 1);
            return closing > 0
                ? (text[1..closing], text[(closing + 1)..].Trim())
                : (text.Trim('"'), string.Empty);
        }

        if (File.Exists(text) || Directory.Exists(text) || !text.Contains(' '))
        {
            return (text, string.Empty);
        }

        var separator = text.IndexOf(' ');
        return (text[..separator], text[(separator + 1)..].Trim());
    }

    /// <summary>
    /// Application bundles and documents go through <c>open</c>, which is what
    /// LaunchServices does for a double click in Finder. Plain executables are
    /// started directly, because <c>open</c> would only reveal them in an editor.
    /// </summary>
    [SupportedOSPlatform("macos")]
    private static void StartOnMac(string path, string? arguments)
    {
        var startInfo = new ProcessStartInfo { UseShellExecute = false };
        if (IsBundle(path) || IsDocument(path))
        {
            startInfo.FileName = OpenTool;
            startInfo.ArgumentList.Add(path);
            if (arguments is { Length: > 0 })
            {
                startInfo.ArgumentList.Add("--args");
                startInfo.ArgumentList.Add(arguments);
            }
        }
        else
        {
            startInfo.FileName = path;
            startInfo.Arguments = arguments ?? string.Empty;
        }

        Process.Start(startInfo)?.Dispose();
    }

    private static bool IsBundle(string path) =>
        path.TrimEnd('/').EndsWith(BundleExtension, StringComparison.OrdinalIgnoreCase) && Directory.Exists(path);

    [SupportedOSPlatform("macos")]
    private static bool IsDocument(string path)
    {
        if (!File.Exists(path))
        {
            // Bare command names such as "ls" are resolved through PATH instead.
            return false;
        }

        const UnixFileMode executable = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
        return (File.GetUnixFileMode(path) & executable) == 0;
    }
}
