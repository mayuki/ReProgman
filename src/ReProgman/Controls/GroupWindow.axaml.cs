using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ReProgman.ViewModels;
using ReProgman.Win31;
using ReProgman.Model;

namespace ReProgman.Controls;

/// <summary>
/// An icon dragged out of its group window, with the pointer position in the
/// workspace canvas coordinates the group windows and icons are placed in.
/// </summary>
public sealed class IconDragEventArgs(ItemViewModel item, Point point, KeyModifiers modifiers) : EventArgs
{
    public ItemViewModel Item { get; } = item;

    public Point Point { get; } = point;

    /// <summary>Ctrl copies instead of moving, as in the original.</summary>
    public bool IsCopy { get; } = modifiers.HasFlag(KeyModifiers.Control);
}

/// <summary>
/// An MDI child window hosted on the workspace canvas. Move/resize are implemented
/// by hand because the children are ordinary controls, not OS windows.
/// </summary>
public partial class GroupWindow : UserControl
{
    private const double MinWindowWidth = 120;
    private const double MinWindowHeight = 80;

    private Point _dragOffset;
    private bool _draggingCaption;
    private ResizeEdge? _resizeEdge;
    private Point _resizeStartPointer;
    private Rect _resizeStartBounds;
    private bool _autoArrange;
    private MenuFlyout? _systemMenu;
    private ItemViewModel? _pressedItem;
    private Point _iconPressPoint;
    private Point _iconPressOffset;
    private Point _iconPressOrigin;
    private bool _draggingIcon;
    private bool _draggingOutside;

    public GroupViewModel Group { get; }

    public bool IsMaximizedChild { get; private set; }

    public event EventHandler? Activated;
    public event EventHandler? MinimizeRequested;
    public event EventHandler? MaximizeRequested;
    public event EventHandler? RestoreRequested;
    public event EventHandler<ItemViewModel>? LaunchRequested;
    public event EventHandler? BoundsChangedByUser;

    /// <summary>An icon is being dragged past this window's edge, over the workspace.</summary>
    public event EventHandler<IconDragEventArgs>? IconDraggedOut;

    /// <summary>The icon came back inside, so the workspace drag ends without a drop.</summary>
    public event EventHandler? IconDragReturned;

    /// <summary>An icon was released outside this window; the workspace decides where it lands.</summary>
    public event EventHandler<IconDragEventArgs>? IconDroppedOut;

    public GroupWindow() : this(new GroupViewModel(new ReProgman.Model.ProgramGroup("Group", [])))
    {
    }

    public GroupWindow(GroupViewModel group)
    {
        Group = group;
        DataContext = group;
        InitializeComponent();

        // Tunnel so a click anywhere (including items) activates the window first.
        AddHandler(PointerPressedEvent, (_, _) => Activated?.Invoke(this, EventArgs.Empty), RoutingStrategies.Tunnel);

        Caption.PointerPressed += OnCaptionPointerPressed;
        Caption.PointerMoved += OnCaptionPointerMoved;
        Caption.PointerReleased += OnCaptionPointerReleased;
        SystemBox.PointerPressed += OnSystemBoxPointerPressed;
        MinButton.Click += (_, _) => MinimizeRequested?.Invoke(this, EventArgs.Empty);
        MaxButton.Click += (_, _) => ToggleMaximize();
        Frame.PointerPressed += OnFramePointerPressed;
        Frame.PointerMoved += OnFramePointerMoved;
        Frame.PointerReleased += OnFramePointerReleased;
        ItemsList.DoubleTapped += OnItemsListDoubleTapped;
        // handledEventsToo: the ListBox marks presses on items as handled while
        // updating the selection, which would otherwise hide them from us.
        ItemsList.AddHandler(PointerPressedEvent, OnItemsListPointerPressed, RoutingStrategies.Bubble, handledEventsToo: true);
        ItemsList.AddHandler(PointerMovedEvent, OnItemsListPointerMoved, RoutingStrategies.Bubble, handledEventsToo: true);
        ItemsList.AddHandler(PointerReleasedEvent, OnItemsListPointerReleased, RoutingStrategies.Bubble, handledEventsToo: true);
        ItemsList.SizeChanged += (_, _) => OnIconAreaChanged();

        // Containers are positioned from code instead of a {Binding} in a
        // ControlTheme setter, which would need reflection (NativeAOT unfriendly).
        ItemsList.ContainerPrepared += (_, e) =>
        {
            if (e.Container is ListBoxItem container && e.Index >= 0 && e.Index < Group.Items.Count)
            {
                ApplyIconPosition(container, Group.Items[e.Index]);
            }
        };
        foreach (var item in Group.Items)
        {
            item.PropertyChanged += OnItemPositionChanged;
        }

        Group.Items.CollectionChanged += OnItemsCollectionChanged;
    }

    private void OnItemsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        // Reorders (drag with Auto Arrange) keep the same item instances; only
        // real additions/removals need subscription and layout updates.
        switch (e.Action)
        {
            case System.Collections.Specialized.NotifyCollectionChangedAction.Add:
                foreach (var item in e.NewItems!.OfType<ItemViewModel>())
                {
                    item.PropertyChanged += OnItemPositionChanged;
                }

                break;
            case System.Collections.Specialized.NotifyCollectionChangedAction.Remove:
                foreach (var item in e.OldItems!.OfType<ItemViewModel>())
                {
                    item.PropertyChanged -= OnItemPositionChanged;
                }

                break;
            default:
                return;
        }

        if (AutoArrange)
        {
            ArrangeIcons();
        }
        else
        {
            PlaceUnpositionedIcons();
        }
    }

    private void OnItemPositionChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is ItemViewModel item &&
            e.PropertyName is nameof(ItemViewModel.IconX) or nameof(ItemViewModel.IconY) &&
            ItemsList.ContainerFromItem(item) is { } container)
        {
            ApplyIconPosition(container, item);
        }
    }

    private void ApplyIconPosition(Control container, ItemViewModel item)
    {
        Canvas.SetLeft(container, item.IconX);
        Canvas.SetTop(container, item.IconY);
        // Canvas.Left/Top only affect arrange, so the IconCanvas would keep its
        // stale extent and the scroll bars would never appear without this.
        ItemsList.ItemsPanelRoot?.InvalidateMeasure();
    }

    /// <summary>
    /// Mirrors Options -> Auto Arrange: while enabled, icons snap to the grid
    /// whenever the window size changes or an icon is dropped.
    /// </summary>
    public bool AutoArrange
    {
        get => _autoArrange;
        set
        {
            _autoArrange = value;
            if (value)
            {
                ArrangeIcons();
            }
        }
    }

    private double IconAreaWidth => ItemsList.Bounds.Width - 20;

    /// <summary>Snaps all icons to the grid in item order (Window -> Arrange Icons).</summary>
    public void ArrangeIcons()
    {
        var width = IconAreaWidth;
        if (width <= 0 || Group.Items.Count == 0)
        {
            return;
        }

        var columns = IconGridLayout.ColumnsFor(width);
        for (var i = 0; i < Group.Items.Count; i++)
        {
            var cell = IconGridLayout.CellAt(i, columns);
            Group.Items[i].SetPosition(cell.X, cell.Y);
        }
    }

    private void OnIconAreaChanged()
    {
        if (IconAreaWidth <= 0 || Group.Items.Count == 0)
        {
            return;
        }

        if (AutoArrange)
        {
            ArrangeIcons();
        }
        else
        {
            PlaceUnpositionedIcons();
        }
    }

    private void PlaceUnpositionedIcons()
    {
        var missing = Group.Items.Where(i => !i.IsPlaced).ToList();
        if (missing.Count == 0)
        {
            return;
        }

        var columns = IconGridLayout.ColumnsFor(IconAreaWidth);
        var occupied = Group.Items
            .Where(i => i.IsPlaced)
            .Select(i => new IconPosition((int)i.IconX, (int)i.IconY));
        var cells = IconGridLayout.PlaceMissing(occupied, missing.Count, columns);
        for (var i = 0; i < missing.Count; i++)
        {
            missing[i].SetPosition(cells[i].X, cells[i].Y);
        }
    }

    public void SetActiveLook(bool isActive)
    {
        Classes.Set("active", isActive);
    }

    /// <summary>
    /// Places an icon dropped from another group. With Auto Arrange the item just
    /// takes the next free cell; otherwise it lands centered under the pointer.
    /// </summary>
    public void PlaceDroppedIcon(ItemViewModel item, Point pointInParent)
    {
        if (AutoArrange ||
            ItemsList.ItemsPanelRoot is not { } canvas ||
            Parent is not Visual host ||
            host.TranslatePoint(pointInParent, canvas) is not { } local)
        {
            item.ClearPosition();
            return;
        }

        item.SetPosition(
            Math.Max(0, Math.Round(local.X - IconGridLayout.CellWidth / 2)),
            Math.Max(0, Math.Round(local.Y - 16)));
    }

    /// <summary>
    /// Follows the frame window: the active child draws its caption inactive
    /// while the application itself is not the foreground window.
    /// </summary>
    public void SetFrameActiveLook(bool frameActive)
    {
        Classes.Set("frameInactive", !frameActive);
    }

    public void SetMaximizedLook(bool maximized)
    {
        IsMaximizedChild = maximized;
        Frame.ShowFrame = !maximized;
        // A maximized MDI child loses its own caption entirely; the system box
        // and restore button move to the parent's menu bar instead.
        Caption.IsVisible = !maximized;
        CaptionLine.IsVisible = !maximized;
        MaxGlyph.Kind = maximized ? ArrowKind.UpDown : ArrowKind.Up;
    }

    public void FocusItems()
    {
        if (ItemsList.SelectedIndex < 0 && ItemsList.ItemCount > 0)
        {
            ItemsList.SelectedIndex = 0;
        }

        if (!ItemsList.Focus())
        {
            // Coming up from Window.Opened the list has not been laid out yet and
            // refuses the focus; retry once the tree is ready.
            Dispatcher.UIThread.Post(() => ItemsList.Focus(), DispatcherPriority.Loaded);
        }
    }

    public ItemViewModel? SelectedItem => ItemsList.SelectedItem as ItemViewModel;

    private void ToggleMaximize()
    {
        if (IsMaximizedChild)
        {
            RestoreRequested?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            MaximizeRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnItemsListDoubleTapped(object? sender, TappedEventArgs e)
    {
        // Ignore double-clicks on empty space or the scroll bar.
        if (e.Source is Visual source &&
            source.FindAncestorOfType<ListBoxItem>(includeSelf: true) is not null &&
            SelectedItem is { } item)
        {
            LaunchRequested?.Invoke(this, item);
        }
    }

    private void OnItemsListPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed ||
            e.ClickCount != 1 ||
            ItemsList.ItemsPanelRoot is not { } canvas ||
            e.Source is not Visual source ||
            source.FindAncestorOfType<ListBoxItem>(includeSelf: true) is not { DataContext: ItemViewModel item })
        {
            return;
        }

        _pressedItem = item;
        _iconPressPoint = e.GetPosition(canvas);
        _iconPressOffset = _iconPressPoint - new Point(item.IconX, item.IconY);
        _iconPressOrigin = new Point(item.IconX, item.IconY);
    }

    private void OnItemsListPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_pressedItem is not { } item || ItemsList.ItemsPanelRoot is not { } canvas)
        {
            return;
        }

        var position = e.GetPosition(canvas);
        if (!_draggingIcon)
        {
            // A small threshold keeps double-clicks from turning into drags.
            var delta = position - _iconPressPoint;
            if (Math.Abs(delta.X) < 5 && Math.Abs(delta.Y) < 5)
            {
                return;
            }

            _draggingIcon = true;
            e.Pointer.Capture(ItemsList);
        }

        // Past the window's own edge the drag belongs to the workspace: the icon
        // goes back where it started (dragging it further would stretch this
        // window's scroll extent) and the parent takes over the visual feedback.
        if (Parent is Visual host)
        {
            var hostPoint = e.GetPosition(host);
            if (!Bounds.Contains(hostPoint))
            {
                if (!_draggingOutside)
                {
                    _draggingOutside = true;
                    item.SetPosition(_iconPressOrigin.X, _iconPressOrigin.Y);
                }

                IconDraggedOut?.Invoke(this, new IconDragEventArgs(item, hostPoint, e.KeyModifiers));
                return;
            }

            if (_draggingOutside)
            {
                _draggingOutside = false;
                IconDragReturned?.Invoke(this, EventArgs.Empty);
            }
        }

        var target = position - _iconPressOffset;
        item.SetPosition(Math.Max(0, Math.Round(target.X)), Math.Max(0, Math.Round(target.Y)));
    }

    private void OnItemsListPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        var item = _pressedItem;
        var dragged = _draggingIcon;
        var outside = _draggingOutside;
        _pressedItem = null;
        _draggingIcon = false;
        _draggingOutside = false;

        if (item is null || !dragged)
        {
            return;
        }

        e.Pointer.Capture(null);
        if (outside)
        {
            var hostPoint = Parent is Visual host ? e.GetPosition(host) : default;
            IconDroppedOut?.Invoke(this, new IconDragEventArgs(item, hostPoint, e.KeyModifiers));
            return;
        }

        if (AutoArrange)
        {
            // Dropping while Auto Arrange is on reorders the items, then snaps
            // everything back onto the grid — just like the original.
            var columns = IconGridLayout.ColumnsFor(IconAreaWidth);
            var oldIndex = Group.Items.IndexOf(item);
            var newIndex = IconGridLayout.IndexAt(item.IconX, item.IconY, columns, Group.Items.Count);
            if (oldIndex >= 0 && oldIndex != newIndex)
            {
                Group.Items.Move(oldIndex, newIndex);
            }

            ArrangeIcons();
            ItemsList.SelectedItem = item;
        }
    }

    /// <summary>
    /// Moves the selection to the nearest icon in the given direction, the way the
    /// original walks its icons. Left and right fall back to the previous or next
    /// item so the end of a row carries on into the neighbouring one.
    /// </summary>
    public bool MoveSelection(int dx, int dy)
    {
        if (Group.Items.Count == 0)
        {
            return false;
        }

        if (SelectedItem is not { } current)
        {
            ItemsList.SelectedIndex = 0;
            ItemsList.ScrollIntoView(ItemsList.SelectedItem!);
            return true;
        }

        var from = Center(current);
        ItemViewModel? best = null;
        var bestScore = double.MaxValue;
        foreach (var item in Group.Items)
        {
            if (ReferenceEquals(item, current))
            {
                continue;
            }

            var to = Center(item);
            var along = (to.X - from.X) * dx + (to.Y - from.Y) * dy;
            if (along <= 0)
            {
                continue;
            }

            // Weighting the sideways distance keeps a row or a column together
            // even when the icons are placed freely.
            var across = Math.Abs((to.X - from.X) * dy) + Math.Abs((to.Y - from.Y) * dx);
            var score = along + across * 4;
            if (score < bestScore)
            {
                bestScore = score;
                best = item;
            }
        }

        if (best is null && dy == 0)
        {
            var index = Group.Items.IndexOf(current) + dx;
            if (index >= 0 && index < Group.Items.Count)
            {
                best = Group.Items[index];
            }
        }


        if (best is null)
        {
            return false;
        }

        ItemsList.SelectedItem = best;
        ItemsList.ScrollIntoView(best);
        return true;
    }

    private static Point Center(ItemViewModel item) =>
        new(item.IconX + IconGridLayout.CellWidth / 2, item.IconY + IconGridLayout.CellHeight / 2);
    private void OnSystemBoxPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
        if (e.ClickCount == 2)
        {
            // Double-clicking the system box closes a group window, which for
            // Program Manager groups means minimizing it.
            _systemMenu?.Hide();
            MinimizeRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        // The box stays clickable while the menu is open: without the pass-through
        // the second press of a double-click is swallowed by light dismiss and the
        // close gesture never arrives.
        var flyout = new MenuFlyout
        {
            Placement = PlacementMode.BottomEdgeAlignedLeft,
            OverlayInputPassThroughElement = SystemBox,
        };
        flyout.Items.Add(CreateSystemMenuItem(Strings.SysRestore, IsMaximizedChild, () => RestoreRequested?.Invoke(this, EventArgs.Empty)));
        flyout.Items.Add(CreateSystemMenuItem(Strings.SysMove, false, null));
        flyout.Items.Add(CreateSystemMenuItem(Strings.SysSize, false, null));
        flyout.Items.Add(CreateSystemMenuItem(Strings.SysMinimize, true, () => MinimizeRequested?.Invoke(this, EventArgs.Empty)));
        flyout.Items.Add(CreateSystemMenuItem(Strings.SysMaximize, !IsMaximizedChild, () => MaximizeRequested?.Invoke(this, EventArgs.Empty)));
        flyout.Items.Add(new Separator());
        var close = CreateSystemMenuItem(Strings.SysClose, true, () => MinimizeRequested?.Invoke(this, EventArgs.Empty));
        MenuProps.SetGestureText(close, "Ctrl+F4");
        flyout.Items.Add(close);
        _systemMenu = flyout;
        flyout.ShowAt(SystemBox);
    }

    private static MenuItem CreateSystemMenuItem(string header, bool enabled, Action? action)
    {
        var item = new MenuItem { Header = header, IsEnabled = enabled };
        if (action is not null)
        {
            item.Click += (_, _) => action();
        }

        return item;
    }

    private void OnCaptionPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || IsMaximizedChild)
        {
            return;
        }

        if (e.Source is Visual source && (SystemBox.IsVisualAncestorOf(source) || source == SystemBox))
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }

        _draggingCaption = true;
        _dragOffset = e.GetPosition(Parent as Visual);
        _resizeStartBounds = new Rect(Canvas.GetLeft(this), Canvas.GetTop(this), Width, Height);
        e.Pointer.Capture(Caption);
        e.Handled = true;
    }

    private void OnCaptionPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_draggingCaption || Parent is not Canvas host)
        {
            return;
        }

        var position = e.GetPosition(host);
        var delta = position - _dragOffset;
        var x = _resizeStartBounds.X + delta.X;
        var y = _resizeStartBounds.Y + delta.Y;

        // Keep at least part of the caption reachable so the window can't be lost.
        x = Math.Clamp(x, -Width + 60, Math.Max(0, host.Bounds.Width - 60));
        y = Math.Clamp(y, 0, Math.Max(0, host.Bounds.Height - 20));
        Canvas.SetLeft(this, Math.Round(x));
        Canvas.SetTop(this, Math.Round(y));
    }

    private void OnCaptionPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_draggingCaption)
        {
            _draggingCaption = false;
            e.Pointer.Capture(null);
            BoundsChangedByUser?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnFramePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (IsMaximizedChild || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var edge = Frame.HitTestEdge(e.GetPosition(Frame));
        if (edge is null)
        {
            return;
        }

        _resizeEdge = edge;
        _resizeStartPointer = e.GetPosition(Parent as Visual);
        _resizeStartBounds = new Rect(Canvas.GetLeft(this), Canvas.GetTop(this), Bounds.Width, Bounds.Height);
        e.Pointer.Capture(Frame);
        e.Handled = true;
    }

    private void OnFramePointerMoved(object? sender, PointerEventArgs e)
    {
        if (_resizeEdge is null)
        {
            UpdateResizeCursor(e);
            return;
        }

        if (Parent is not Canvas host)
        {
            return;
        }

        var delta = e.GetPosition(host) - _resizeStartPointer;
        var start = new ResizeRect(_resizeStartBounds.X, _resizeStartBounds.Y, _resizeStartBounds.Width, _resizeStartBounds.Height);
        var rect = WindowResize.Drag(start, _resizeEdge.Value, delta.X, delta.Y, MinWindowWidth, MinWindowHeight);

        Canvas.SetLeft(this, Math.Round(rect.X));
        Canvas.SetTop(this, Math.Round(rect.Y));
        Width = Math.Round(rect.Width);
        Height = Math.Round(rect.Height);
    }

    private void OnFramePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_resizeEdge is not null)
        {
            _resizeEdge = null;
            e.Pointer.Capture(null);
            BoundsChangedByUser?.Invoke(this, EventArgs.Empty);
        }
    }

    private void UpdateResizeCursor(PointerEventArgs e)
    {
        if (IsMaximizedChild)
        {
            Frame.Cursor = Cursor.Default;
            return;
        }

        Frame.Cursor = Frame.HitTestEdge(e.GetPosition(Frame)) switch
        {
            ResizeEdge.North or ResizeEdge.South => new Cursor(StandardCursorType.SizeNorthSouth),
            ResizeEdge.West or ResizeEdge.East => new Cursor(StandardCursorType.SizeWestEast),
            ResizeEdge.NorthWest or ResizeEdge.SouthEast => new Cursor(StandardCursorType.TopLeftCorner),
            ResizeEdge.NorthEast or ResizeEdge.SouthWest => new Cursor(StandardCursorType.TopRightCorner),
            _ => Cursor.Default,
        };
    }
}