using System.Globalization;
using Avalonia;
using ReProgman.Services;
using ReProgman.Model;

namespace ReProgman;

internal static class Program
{
    // Avalonia requires STA on Windows; shell icon extraction also relies on it.
    [STAThread]
    public static int Main(string[] args)
    {
        // The display language must be fixed before any XAML reads the catalog.
        var settings = SettingsStore.Load();
        var language = DisplayLanguage.Resolve(settings.Language, CultureInfo.CurrentUICulture.Name);
        Strings.UseJapanese(language == DisplayLanguage.Japanese);

        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        // Overlay popups keep opened menus inside the window surface so that
        // the screenshot debug mode can capture them. The capture renders into
        // a 96-DPI bitmap, so pixel snapping must ignore the monitor's scale.
        var overlayPopups = Environment.GetCommandLineArgs().Contains("--screenshot");
        if (overlayPopups)
        {
            Win31.PixelSnap.Override = 1.0;
        }

        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new Win32PlatformOptions { OverlayPopups = overlayPopups })
            .With(new AvaloniaNativePlatformOptions { OverlayPopups = overlayPopups })
            .LogToTrace();
    }
}
