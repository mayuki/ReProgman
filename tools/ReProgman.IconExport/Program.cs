using Avalonia;

namespace ReProgman.IconExport;

/// <summary>
/// Renders the committed application icons from the group icon the app draws for
/// itself. Run through build/export-icons.ps1:
///
///   dotnet run --project tools/ReProgman.IconExport -- --ico &lt;path&gt; --icns &lt;path&gt;
/// </summary>
internal static class Program
{
    // Avalonia requires STA on Windows.
    [STAThread]
    public static int Main(string[] args)
    {
        var ico = ParseArgValue(args, "--ico");
        var icns = ParseArgValue(args, "--icns");
        if (ico is null && icns is null)
        {
            Console.Error.WriteLine("usage: ReProgman.IconExport [--ico <path>] [--icns <path>]");
            return 1;
        }

        // The drawing needs a live rendering backend, so Avalonia is started up
        // with the plain Application class; no window is ever shown and the art
        // draws itself without any styles.
        AppBuilder.Configure<Application>()
            .UsePlatformDetect()
            .With(new MacOSPlatformOptions { ShowInDock = false })
            .SetupWithoutStarting();

        if (ico is not null)
        {
            IconAssets.WriteIco(ico);
            Report(ico);
        }

        if (icns is not null)
        {
            IconAssets.WriteIcns(icns);
            Report(icns);
        }

        return 0;
    }

    private static void Report(string path) =>
        Console.WriteLine($"{path} ({new FileInfo(path).Length} bytes)");

    private static string? ParseArgValue(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name)
            {
                return args[i + 1];
            }
        }

        return null;
    }
}
