using Avalonia;
using Avalonia.Controls;

namespace ReProgman.Win31;

/// <summary>
/// Attached property carrying the shortcut text shown right-aligned in menus.
/// Windows 3.1 wrote abbreviations like "Del" and "Alt+Enter" that the standard
/// KeyGesture formatter cannot reproduce, so the text is authored by hand.
/// </summary>
public static class MenuProps
{
    public static readonly AttachedProperty<string?> GestureTextProperty =
        AvaloniaProperty.RegisterAttached<MenuItem, string?>("GestureText", typeof(MenuProps));

    public static string? GetGestureText(MenuItem element) => element.GetValue(GestureTextProperty);

    public static void SetGestureText(MenuItem element, string? value) => element.SetValue(GestureTextProperty, value);
}
