using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace ReProgman.Views;

/// <summary>Base class for all borderless modal dialogs with Windows 3.1 chrome.</summary>
public class Win31DialogWindow : Window
{
    protected Win31DialogWindow()
    {
        SystemDecorations = SystemDecorations.None;
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;
        ShowInTaskbar = false;
        ShowActivated = true;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        RenderOptions.SetTextRenderingMode(this, TextRenderingMode.Alias);
        RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }
}
