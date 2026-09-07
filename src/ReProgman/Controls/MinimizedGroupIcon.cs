using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using ReProgman.Win31;

namespace ReProgman.Controls;

/// <summary>A minimized group shown as an icon with a label at the bottom of the workspace.</summary>
public class MinimizedGroupIcon : Border
{
    private readonly Border _labelBorder;
    private readonly TextBlock _label;
    private MenuFlyout? _systemMenu;

    public string Title { get; private set; }

    public event EventHandler? Activated;
    public event EventHandler? RestoreRequested;
    public event EventHandler? MaximizeRequested;

    public MinimizedGroupIcon(string title)
    {
        Title = title;
        Width = 76;
        Background = Brushes.Transparent;

        _label = new TextBlock
        {
            Text = title,
            FontFamily = new FontFamily("Microsoft Sans Serif, MS Gothic, Yu Gothic UI"),
            FontSize = 11,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 74,
            MaxLines = 2,
        };
        _labelBorder = new Border
        {
            Child = _label,
            Padding = new Thickness(2, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        Child = new StackPanel
        {
            // Transparent but hit-testable: the light dismiss pass-through only
            // forwards presses hitting a DESCENDANT of the icon, so without this
            // the second click of a double-click on empty space is swallowed.
            Background = Brushes.Transparent,
            Spacing = 3,
            Children =
            {
                new GroupIconArt { Width = 32, Height = 32, HorizontalAlignment = HorizontalAlignment.Center },
                _labelBorder,
            },
        };

        PointerPressed += OnPointerPressed;
        DoubleTapped += OnDoubleTapped;
    }

    public void UpdateTitle(string title)
    {
        Title = title;
        _label.Text = title;
    }

    public void SetActiveLook(bool isActive)
    {
        _labelBorder.Background = isActive ? Win31Palette.ActiveTitleBrush : Brushes.Transparent;
        _label.Foreground = isActive ? Brushes.White : Brushes.Black;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        Activated?.Invoke(this, EventArgs.Empty);
        if (e.ClickCount != 1)
        {
            _systemMenu?.Hide();
            return;
        }

        // Like the original, a single click pops up the icon's system menu.
        // The icon itself must stay clickable while the flyout is open: without
        // OverlayInputPassThroughElement, the second press of a double-click is
        // swallowed by light dismiss and DoubleTapped (restore) never fires.
        var flyout = new MenuFlyout
        {
            Placement = PlacementMode.TopEdgeAlignedLeft,
            OverlayInputPassThroughElement = this,
        };
        var restore = new MenuItem { Header = Strings.SysRestore };
        restore.Click += (_, _) => RestoreRequested?.Invoke(this, EventArgs.Empty);
        var maximize = new MenuItem { Header = Strings.SysMaximize };
        maximize.Click += (_, _) => MaximizeRequested?.Invoke(this, EventArgs.Empty);
        flyout.Items.Add(restore);
        flyout.Items.Add(maximize);
        _systemMenu = flyout;
        flyout.ShowAt(this);
    }

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        RestoreRequested?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }
}
