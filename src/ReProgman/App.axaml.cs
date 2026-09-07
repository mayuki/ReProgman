using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using ReProgman.Views;

namespace ReProgman;

public class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        // Popups (menus, flyouts) are separate top-levels, so the render
        // options set on a window never reach them; without this hook menu
        // text is drawn antialiased and looks blurry and heavy.
        Control.LoadedEvent.AddClassHandler<TopLevel>((topLevel, _) =>
        {
            RenderOptions.SetTextRenderingMode(topLevel, TextRenderingMode.Alias);
            RenderOptions.SetEdgeMode(topLevel, EdgeMode.Aliased);
        });

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow(desktop.Args ?? []);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
