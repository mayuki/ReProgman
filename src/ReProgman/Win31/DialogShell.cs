using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;

namespace ReProgman.Win31;

/// <summary>
/// The chrome shared by all modal dialogs: fixed dialog frame, navy caption with a
/// system box, and the content area. Hosted inside a borderless Window.
/// </summary>
public class DialogShell : ContentControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<DialogShell, string>(nameof(Title), "");

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (e.NameScope.Find<Control>("PART_Caption") is { } caption)
        {
            caption.PointerPressed += OnCaptionPointerPressed;
        }

        if (e.NameScope.Find<Control>("PART_SystemBox") is { } systemBox)
        {
            systemBox.PointerPressed += OnSystemBoxPointerPressed;
        }
    }

    private void OnCaptionPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (VisualRoot is Window window && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            window.BeginMoveDrag(e);
        }
    }

    private void OnSystemBoxPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Double-clicking the system box closes the dialog, i.e. "Cancel".
        if (e.ClickCount == 2 && VisualRoot is Window window)
        {
            window.Close();
            e.Handled = true;
        }
    }
}
