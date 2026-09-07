using System.Runtime.Versioning;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ReProgman.Controls;
using ReProgman.Services;
using ReProgman.ViewModels;
using ReProgman.Win31;
using ReProgman.Model;

namespace ReProgman.Views;

public partial class MainWindow : Window
{
    private sealed class GroupEntry(GroupViewModel vm, GroupWindow window)
    {
        public GroupViewModel Vm { get; } = vm;
        public GroupWindow Window { get; } = window;
        public MinimizedGroupIcon? Icon { get; set; }
        public WindowStateKind State { get; set; } = WindowStateKind.Minimized;
        public Rect NormalBounds { get; set; }
    }

    private readonly List<GroupEntry> _groups = [];
    private readonly ReProgmanSettings _settings;
    private readonly string? _screenshotPath;
    private readonly string? _screenshotScene;
    private GroupEntry? _active;
    // Minimized icons keep the default 0 so they stay on the workspace floor.
    private int _topZIndex;
    private Control? _dragGhost;
    private MenuFlyout? _systemMenu;
    private bool _exitConfirmed;
    private bool _mdiMaximized;
    private bool _isCustomMaximized;
    private Rect? _normalWindowBounds;
    private ResizeEdge? _resizeEdge;
    private Point _resizeStartPointer;
    private ResizeRect _resizeStartBounds;

    /// <summary>
    /// Avalonia's macOS backend implements <c>BeginResizeDrag</c> as an empty
    /// method, so there the sizing frame has to run the drag itself, the way the
    /// MDI children already do. Windows and X11 keep the native drag, which
    /// brings the window manager's own snapping with it.
    /// </summary>
    [SupportedOSPlatformGuard("macos")]
    private static bool UsesManualResize => OperatingSystem.IsMacOS();

    public MainWindow() : this([])
    {
    }

    public MainWindow(string[] args)
    {
        _screenshotPath = ParseArgValue(args, "--screenshot");
        _screenshotScene = ParseArgValue(args, "--scene");
        _settings = SettingsStore.Load();

        InitializeComponent();
        RenderOptions.SetTextRenderingMode(this, TextRenderingMode.Alias);
        RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
        Icon = CreateWindowIcon();

        ApplyMainWindowPlacement();
        WireChrome();
        WireKeyBindings();

        MenuAutoArrange.IsChecked = _settings.AutoArrange;
        MenuMinimizeOnUse.IsChecked = _settings.MinimizeOnUse;
        MenuSaveSettings.IsChecked = _settings.SaveSettingsOnExit;
        // A top-level MenuItem without children never opens a popup, so the
        // Window menu must be populated before its first click as well.
        RebuildWindowMenu();
        MenuWindow.SubmenuOpened += (_, _) => RebuildWindowMenu();
        MenuFileTop.SubmenuOpened += (_, _) => UpdateFileMenuState();

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // The macOS application menu and Cmd+Q ask the lifetime to shut down.
            // Route that through the Windows 3.1 exit confirmation as well.
            desktop.ShutdownRequested += OnShutdownRequested;
        }

        Activated += (_, _) => SetFrameActiveLook(true);
        Deactivated += (_, _) => SetFrameActiveLook(false);
        MdiCanvas.SizeChanged += (_, _) => OnWorkspaceResized();
        // The arrows move the icon selection of the active group, wherever the
        // focus happens to sit; an open menu marks them handled first, so menu
        // navigation still wins.
        AddHandler(KeyDownEvent, OnArrowKeyDown, RoutingStrategies.Bubble);
        Opened += OnWindowOpened;
    }

    /// <summary>
    /// Paints the frame and the active group window as active or inactive
    /// together: in the original MDI the child's caption follows the frame.
    /// </summary>
    private void SetFrameActiveLook(bool active)
    {
        Classes.Set("inactiveWin", !active);
        foreach (var entry in _groups)
        {
            entry.Window.SetFrameActiveLook(active);
        }
    }

    private void OnArrowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.None || _active is not { State: not WindowStateKind.Minimized } active)
        {
            return;
        }

        var (dx, dy) = e.Key switch
        {
            Key.Left => (-1, 0),
            Key.Right => (1, 0),
            Key.Up => (0, -1),
            Key.Down => (0, 1),
            _ => (0, 0),
        };

        if ((dx != 0 || dy != 0) && active.Window.MoveSelection(dx, dy))
        {
            e.Handled = true;
        }
    }

    private static WindowIcon CreateWindowIcon()
    {
        // The taskbar icon is our hand-drawn group icon, rendered once at 32x32.
        var bitmap = GroupIconArt.RenderToBitmap(32);
        using var stream = new MemoryStream();
        bitmap.Save(stream);
        stream.Position = 0;
        return new WindowIcon(stream);
    }

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

    // ------------------------------------------------------------------ chrome

    private void WireChrome()
    {
        MinButton.Click += (_, _) => WindowState = WindowState.Minimized;
        MaxButton.Click += (_, _) => ToggleMaximize();

        Caption.PointerPressed += OnCaptionPointerPressed;
        SystemBox.PointerPressed += OnSystemBoxPointerPressed;
        Frame.PointerPressed += OnFramePointerPressed;
        Frame.PointerMoved += OnFramePointerMoved;
        Frame.PointerReleased += OnFramePointerReleased;
        Frame.PointerCaptureLost += (_, _) => _resizeEdge = null;
        MdiSystemBox.PointerPressed += OnMdiSystemBoxPointerPressed;
        MdiRestoreButton.Click += (_, _) =>
        {
            if (MaximizedEntry is { } entry)
            {
                RestoreGroup(entry);
            }
        };
    }

    private void OnMdiSystemBoxPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
        if (MaximizedEntry is not { } entry)
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            // Closing a group means minimizing it, like its own system box.
            _systemMenu?.Hide();
            MinimizeGroup(entry);
            return;
        }

        var flyout = new MenuFlyout
        {
            Placement = PlacementMode.BottomEdgeAlignedLeft,
            OverlayInputPassThroughElement = MdiSystemBox,
        };
        flyout.Items.Add(MakeMenuItem(Strings.SysRestore, true, () => RestoreGroup(entry)));
        flyout.Items.Add(MakeMenuItem(Strings.SysMove, false, null));
        flyout.Items.Add(MakeMenuItem(Strings.SysSize, false, null));
        flyout.Items.Add(MakeMenuItem(Strings.SysMinimize, true, () => MinimizeGroup(entry)));
        flyout.Items.Add(MakeMenuItem(Strings.SysMaximize, false, null));
        flyout.Items.Add(new Separator());
        flyout.Items.Add(MakeMenuItem(Strings.SysClose, true, () => MinimizeGroup(entry), "Ctrl+F4"));
        _systemMenu = flyout;
        flyout.ShowAt(MdiSystemBox);
    }

    private void OnCaptionPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (e.Source is Visual source && (source == SystemBox || SystemBox.IsVisualAncestorOf(source)))
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }

        if (!_isCustomMaximized)
        {
            BeginMoveDrag(e);
        }
    }

    private void OnSystemBoxPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        e.Handled = true;
        if (e.ClickCount == 2)
        {
            _systemMenu?.Hide();
            RequestExit();
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
        flyout.Items.Add(MakeMenuItem(Strings.SysRestore, _isCustomMaximized, () => SetMaximized(false)));
        flyout.Items.Add(MakeMenuItem(Strings.SysMove, false, null));
        flyout.Items.Add(MakeMenuItem(Strings.SysSize, false, null));
        flyout.Items.Add(MakeMenuItem(Strings.SysMinimize, true, () => WindowState = WindowState.Minimized));
        flyout.Items.Add(MakeMenuItem(Strings.SysMaximize, !_isCustomMaximized, () => SetMaximized(true)));
        flyout.Items.Add(new Separator());
        flyout.Items.Add(MakeMenuItem(Strings.SysClose, true, RequestExit, "Alt+F4"));
        flyout.Items.Add(new Separator());
        flyout.Items.Add(MakeMenuItem(Strings.SysSwitchTo, false, null, "Ctrl+Esc"));
        _systemMenu = flyout;
        flyout.ShowAt(SystemBox);
    }

    private static MenuItem MakeMenuItem(string header, bool enabled, Action? action, string? gesture = null)
    {
        var item = new MenuItem { Header = header, IsEnabled = enabled };
        if (gesture is not null)
        {
            MenuProps.SetGestureText(item, gesture);
        }

        if (action is not null)
        {
            item.Click += (_, _) => action();
        }

        return item;
    }

    private void OnFramePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_isCustomMaximized || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (Frame.HitTestEdge(e.GetPosition(Frame)) is not { } edge)
        {
            return;
        }

        if (UsesManualResize && MacPointer.TryGetScreenPosition(out var pointer))
        {
            _resizeEdge = edge;
            _resizeStartPointer = pointer;
            _resizeStartBounds = new ResizeRect(Position.X, Position.Y, ClientSize.Width, ClientSize.Height);
            e.Pointer.Capture(Frame);
        }
        else
        {
            BeginResizeDrag(ToWindowEdge(edge), e);
        }

        e.Handled = true;
    }

    private void OnFramePointerMoved(object? sender, PointerEventArgs e)
    {
        if (_resizeEdge is { } edge)
        {
            ResizeTo(edge);
            return;
        }

        if (_isCustomMaximized)
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

    /// <summary>
    /// Runs one step of a manual sizing drag. Both the pointer and the rectangle
    /// the drag started from are anchored on the screen, and nothing here reads
    /// the window's own geometry back: a west or north drag moves the window, so
    /// measuring against anything that moves with it turns the drag into an
    /// undamped feedback loop and the window visibly shakes.
    /// On macOS one point is one device-independent pixel, which is what lets
    /// the screen-space delta be applied to the size directly.
    /// </summary>
    private void ResizeTo(ResizeEdge edge)
    {
        if (!UsesManualResize || !MacPointer.TryGetScreenPosition(out var pointer))
        {
            return;
        }

        // Round the delta rather than the results, so that the edges the drag
        // did not grab land on exactly the coordinate they started at.
        var rect = WindowResize.Drag(
            _resizeStartBounds,
            edge,
            Math.Round(pointer.X - _resizeStartPointer.X),
            Math.Round(pointer.Y - _resizeStartPointer.Y),
            MinWidth,
            MinHeight);

        Position = new PixelPoint((int)rect.X, (int)rect.Y);
        Width = rect.Width;
        Height = rect.Height;
    }

    private void OnFramePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_resizeEdge is null)
        {
            return;
        }

        _resizeEdge = null;
        e.Pointer.Capture(null);
    }

    private static WindowEdge ToWindowEdge(ResizeEdge edge) => edge switch
    {
        ResizeEdge.North => WindowEdge.North,
        ResizeEdge.South => WindowEdge.South,
        ResizeEdge.West => WindowEdge.West,
        ResizeEdge.East => WindowEdge.East,
        ResizeEdge.NorthWest => WindowEdge.NorthWest,
        ResizeEdge.NorthEast => WindowEdge.NorthEast,
        ResizeEdge.SouthWest => WindowEdge.SouthWest,
        _ => WindowEdge.SouthEast,
    };

    private void ToggleMaximize() => SetMaximized(!_isCustomMaximized);

    private void SetMaximized(bool maximized)
    {
        if (_isCustomMaximized == maximized)
        {
            return;
        }

        _isCustomMaximized = maximized;
        if (maximized)
        {
            _normalWindowBounds = new Rect(Position.X, Position.Y, Width, Height);
            WindowState = WindowState.Maximized;
        }
        else
        {
            WindowState = WindowState.Normal;
            if (_normalWindowBounds is { } bounds)
            {
                Position = new PixelPoint((int)bounds.X, (int)bounds.Y);
                Width = bounds.Width;
                Height = bounds.Height;
            }
        }

        // Windows 3.1 removes the sizing frame entirely while maximized.
        Frame.ShowFrame = !maximized;
        MaxGlyph.Kind = maximized ? ArrowKind.UpDown : ArrowKind.Up;
    }

    // ------------------------------------------------------------------- keys

    private void WireKeyBindings()
    {
        void Bind(KeyGesture gesture, Action action) =>
            KeyBindings.Add(new KeyBinding { Gesture = gesture, Command = new RelayCommand(action) });

        Bind(new KeyGesture(Key.Enter), OpenSelected);
        Bind(new KeyGesture(Key.F5, KeyModifiers.Shift), CascadeGroups);
        Bind(new KeyGesture(Key.F4, KeyModifiers.Shift), TileGroups);
        Bind(new KeyGesture(Key.F6, KeyModifiers.Control), ActivateNextGroup);
        Bind(new KeyGesture(Key.Tab, KeyModifiers.Control), ActivateNextGroup);
        Bind(new KeyGesture(Key.F4, KeyModifiers.Control), MinimizeActiveGroup);
        Bind(new KeyGesture(Key.Delete), () => _ = DeleteSelection());
        Bind(new KeyGesture(Key.F7), () => _ = MoveOrCopySelectedItem(isCopy: false));
        Bind(new KeyGesture(Key.F8), () => _ = MoveOrCopySelectedItem(isCopy: true));
        Bind(new KeyGesture(Key.Enter, KeyModifiers.Alt), () => _ = EditProperties());
    }

    // ------------------------------------------------------------------- menu

    private void OnMenuOpenClicked(object? sender, RoutedEventArgs e) => OpenSelected();

    private async void OnMenuRunClicked(object? sender, RoutedEventArgs e)
    {
        var dialog = new RunDialog();
        var accepted = await dialog.ShowDialog<bool>(this);
        if (accepted && dialog.CommandLine is { } commandLine)
        {
            try
            {
                ShellLauncher.LaunchCommandLine(commandLine, dialog.RunMinimized);
                if (_settings.MinimizeOnUse)
                {
                    WindowState = WindowState.Minimized;
                }
            }
            catch (Exception)
            {
                await ShowLaunchError(commandLine);
            }
        }
    }

    private void OnMenuExitClicked(object? sender, RoutedEventArgs e) => RequestExit();

    private async void OnMenuAboutClicked(object? sender, RoutedEventArgs e) =>
        await new AboutDialog().ShowDialog(this);

    private void OnOptionToggled(object? sender, RoutedEventArgs e)
    {
        _settings.AutoArrange = MenuAutoArrange.IsChecked;
        _settings.MinimizeOnUse = MenuMinimizeOnUse.IsChecked;
        _settings.SaveSettingsOnExit = MenuSaveSettings.IsChecked;

        foreach (var entry in _groups)
        {
            entry.Window.AutoArrange = _settings.AutoArrange;
        }
    }

    private void RebuildWindowMenu()
    {
        MenuWindow.Items.Clear();
        MenuWindow.Items.Add(MakeMenuItem(Strings.MenuCascade, true, CascadeGroups, "Shift+F5"));
        MenuWindow.Items.Add(MakeMenuItem(Strings.MenuTile, true, TileGroups, "Shift+F4"));
        MenuWindow.Items.Add(MakeMenuItem(Strings.MenuArrangeIcons, true, ArrangeActiveIcons));
        if (_groups.Count == 0)
        {
            return;
        }

        MenuWindow.Items.Add(new Separator());
        for (var i = 0; i < _groups.Count; i++)
        {
            var entry = _groups[i];
            var prefix = i < 9 ? $"_{i + 1} " : $"{i + 1} ";
            var item = new MenuItem
            {
                Header = prefix + entry.Vm.Name,
                ToggleType = MenuItemToggleType.CheckBox,
                IsChecked = entry == _active,
            };
            item.Click += (_, _) =>
            {
                if (entry.State == WindowStateKind.Minimized)
                {
                    OpenGroup(entry);
                }
                else
                {
                    ActivateGroup(entry);
                }
            };
            MenuWindow.Items.Add(item);
        }
    }

    // ---------------------------------------------------------------- startup

    private void OnWindowOpened(object? sender, EventArgs e)
    {
        if (_settings.MainWindow is { State: WindowStateKind.Maximized })
        {
            SetMaximized(true);
        }

        LoadGroupsAsync();

        if (_screenshotPath is not null)
        {
            DispatcherTimer.RunOnce(PrepareScreenshotScene, TimeSpan.FromMilliseconds(1200));
            DispatcherTimer.RunOnce(CaptureScreenshotAndExit, TimeSpan.FromMilliseconds(2000));
        }
    }

    private void ApplyMainWindowPlacement()
    {
        if (_settings.MainWindow is { } placement && placement.State != WindowStateKind.Maximized)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Position = new PixelPoint(placement.X, placement.Y);
            Width = Math.Max(MinWidth, placement.Width);
            Height = Math.Max(MinHeight, placement.Height);
        }
    }

    /// <summary>
    /// The folders the initial groups are built from. macOS mirrors the Windows
    /// per-user / machine-wide pair, with the user's own folder first so that a
    /// privately installed application wins over the system copy.
    /// </summary>
    private static string[] StartMenuRoots => OperatingSystem.IsMacOS()
        ?
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Applications"),
            "/Applications",
            "/System/Applications",
        ]
        :
        [
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
        ];

    private void LoadGroupsAsync()
    {
        // Groups.ini is the source of truth once it exists; the Start Menu is
        // only scanned to create it on first run (and on explicit refresh).
        if (GroupsStore.Exists)
        {
            BuildGroups(GroupsStore.Load());
            return;
        }

        var roots = StartMenuRoots;
        var mainGroupName = Strings.MainGroupName;
        Task.Run(() => new StartMenuScanner(AppEntrySources.ForCurrentPlatform()).Scan(roots, mainGroupName)).ContinueWith(
            task =>
            {
                var groups = task.IsCompletedSuccessfully ? task.Result : [];
                if (_screenshotPath is null)
                {
                    GroupsStore.Save(groups);
                }

                BuildGroups(groups);
            },
            TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void BuildGroups(IReadOnlyList<ProgramGroup> groups)
    {
        var ordered = groups
            .OrderBy(g =>
            {
                var index = _settings.GroupOrder.FindIndex(n => string.Equals(n, g.Name, StringComparison.OrdinalIgnoreCase));
                return index < 0 ? int.MaxValue : index;
            })
            .ToList();

        var hostWidth = Math.Max(300, MdiCanvas.Bounds.Width);
        var hostHeight = Math.Max(200, MdiCanvas.Bounds.Height);
        var isFirstRun = _settings.GroupWindows.Count == 0;

        for (var i = 0; i < ordered.Count; i++)
        {
            var entry = CreateEntry(new GroupViewModel(ordered[i]));

            if (_settings.GroupWindows.TryGetValue(entry.Vm.Name, out var placement))
            {
                entry.State = placement.State;
                entry.NormalBounds = placement is { Width: > 50, Height: > 40 }
                    ? new Rect(placement.X, placement.Y, placement.Width, placement.Height)
                    : DefaultBounds(i);
            }
            else
            {
                entry.State = isFirstRun && i == 0 ? WindowStateKind.Normal : WindowStateKind.Minimized;
                entry.NormalBounds = isFirstRun && i == 0
                    ? new Rect(8, 8, Math.Min(560, hostWidth * 0.8), Math.Min(280, hostHeight * 0.62))
                    : DefaultBounds(i);
            }
        }

        foreach (var entry in _groups)
        {
            switch (entry.State)
            {
                case WindowStateKind.Normal:
                    ShowGroupWindow(entry);
                    break;
                case WindowStateKind.Maximized:
                    ShowGroupWindow(entry);
                    entry.State = WindowStateKind.Maximized;
                    ApplyMaximizedBounds(entry);
                    _mdiMaximized = true;
                    break;
                default:
                    ShowGroupIcon(entry);
                    break;
            }
        }

        ArrangeMinimizedIcons();

        var toActivate =
            _groups.FirstOrDefault(g => string.Equals(g.Vm.Name, _settings.ActiveGroup, StringComparison.OrdinalIgnoreCase))
            ?? _groups.FirstOrDefault(g => g.State != WindowStateKind.Minimized)
            ?? _groups.FirstOrDefault();
        if (toActivate is not null)
        {
            ActivateGroup(toActivate);
        }

        UpdateMdiChrome();
        StartIconLoading();

        Rect DefaultBounds(int index) => new(
            12 + (index % 6) * 24,
            10 + (index % 6) * 22,
            Math.Min(360, hostWidth * 0.55),
            Math.Min(220, hostHeight * 0.5));
    }

    private GroupEntry CreateEntry(GroupViewModel vm)
    {
        var window = new GroupWindow(vm);
        var entry = new GroupEntry(vm, window);

        if (_settings.IconPositions.TryGetValue(vm.Name, out var iconPositions))
        {
            foreach (var item in vm.Items)
            {
                if (iconPositions.TryGetValue(item.Name, out var position))
                {
                    item.SetPosition(position.X, position.Y);
                }
            }
        }

        window.AutoArrange = _settings.AutoArrange;
        window.SetFrameActiveLook(IsActive);
        window.Activated += (_, _) => ActivateGroup(entry);
        window.IconDraggedOut += (_, args) => ShowDragGhost(args);
        window.IconDragReturned += (_, _) => HideDragGhost();
        window.IconDroppedOut += (_, args) => OnIconDroppedOut(entry, args);
        window.MinimizeRequested += (_, _) => MinimizeGroup(entry);
        window.MaximizeRequested += (_, _) => MaximizeGroup(entry);
        window.RestoreRequested += (_, _) => RestoreGroup(entry);
        window.LaunchRequested += (_, item) => _ = LaunchItem(item);
        window.BoundsChangedByUser += (_, _) => entry.NormalBounds = GetWindowBounds(entry);

        _groups.Add(entry);
        return entry;
    }

    private void StartIconLoading() => LoadIconsAsync(_groups.SelectMany(g => g.Vm.Items).ToList());

    private static void LoadIconsAsync(IReadOnlyList<ItemViewModel> items)
    {
        if (items.Count == 0)
        {
            return;
        }

        // Icons load on a dedicated thread so the UI stays responsive while
        // hundreds of them are decoded.
        var thread = new Thread(() =>
        {
            foreach (var item in items)
            {
                Bitmap? bitmap = null;
                try
                {
                    bitmap = IconLoader.GetLargeIcon(item.Path);
                }
                catch
                {
                    // Ignore: the generic group icon remains visible.
                }

                if (bitmap is not null)
                {
                    Dispatcher.UIThread.Post(() => item.Icon = bitmap);
                }
            }
        })
        {
            IsBackground = true,
            Name = "IconLoader",
        };

        if (OperatingSystem.IsWindows())
        {
            // Windows shell icon extraction resolves .lnk targets through COM,
            // which wants an STA thread. Other platforms have no apartments.
            thread.SetApartmentState(ApartmentState.STA);
        }

        thread.Start();
    }

    // -------------------------------------------------------------------- MDI

    private static Rect GetWindowBounds(GroupEntry entry) => new(
        Canvas.GetLeft(entry.Window),
        Canvas.GetTop(entry.Window),
        entry.Window.Bounds.Width,
        entry.Window.Bounds.Height);

    private void ShowGroupWindow(GroupEntry entry)
    {
        if (entry.Icon is not null)
        {
            MdiCanvas.Children.Remove(entry.Icon);
            entry.Icon = null;
        }

        entry.Window.SetMaximizedLook(false);
        Canvas.SetLeft(entry.Window, entry.NormalBounds.X);
        Canvas.SetTop(entry.Window, entry.NormalBounds.Y);
        entry.Window.Width = entry.NormalBounds.Width;
        entry.Window.Height = entry.NormalBounds.Height;
        if (!MdiCanvas.Children.Contains(entry.Window))
        {
            MdiCanvas.Children.Add(entry.Window);
        }

        RaiseGroupWindow(entry);

        if (entry.State == WindowStateKind.Minimized)
        {
            entry.State = WindowStateKind.Normal;
        }
    }

    /// <summary>
    /// Puts a group window on top of the other children of the workspace. Every
    /// call takes a fresh number: comparing against the current top first would
    /// never fire, because a window that has not been raised yet sits at 0 just
    /// like the counter does.
    /// </summary>
    private void RaiseGroupWindow(GroupEntry entry) => entry.Window.ZIndex = ++_topZIndex;

    private void ShowGroupIcon(GroupEntry entry)
    {
        MdiCanvas.Children.Remove(entry.Window);
        entry.State = WindowStateKind.Minimized;

        if (entry.Icon is null)
        {
            var icon = new MinimizedGroupIcon(entry.Vm.Name);
            icon.Activated += (_, _) => ActivateGroup(entry);
            icon.RestoreRequested += (_, _) => OpenGroup(entry);
            icon.MaximizeRequested += (_, _) => MaximizeGroup(entry);
            entry.Icon = icon;
            // Icons live on the workspace floor, always below open group windows.
            MdiCanvas.Children.Insert(0, icon);
        }
    }

    private void ActivateGroup(GroupEntry entry)
    {
        // In maximized MDI mode the zoomed state follows the active child:
        // activating another open group hands the whole workspace over to it.
        if (_mdiMaximized &&
            entry.State == WindowStateKind.Normal &&
            MaximizedEntry is { } current &&
            current != entry)
        {
            current.State = WindowStateKind.Normal;
            current.Window.SetMaximizedLook(false);
            Canvas.SetLeft(current.Window, current.NormalBounds.X);
            Canvas.SetTop(current.Window, current.NormalBounds.Y);
            current.Window.Width = current.NormalBounds.Width;
            current.Window.Height = current.NormalBounds.Height;

            entry.NormalBounds = GetWindowBounds(entry);
            entry.State = WindowStateKind.Maximized;
            ApplyMaximizedBounds(entry);
            UpdateMdiChrome();
        }

        _active = entry;
        foreach (var other in _groups)
        {
            var isActive = other == entry;
            other.Window.SetActiveLook(isActive && other.State != WindowStateKind.Minimized);
            other.Icon?.SetActiveLook(isActive);
        }

        if (entry.State != WindowStateKind.Minimized && MdiCanvas.Children.Contains(entry.Window))
        {
            // Raised through ZIndex rather than by re-adding the control: taking
            // it out of the canvas mid-click detaches the visual the press
            // started on, and the rest of that event (an icon drag, say) is lost.
            RaiseGroupWindow(entry);
        }

        if (entry.State != WindowStateKind.Minimized)
        {
            entry.Window.FocusItems();
        }
    }

    private GroupEntry? MaximizedEntry => _groups.FirstOrDefault(g => g.State == WindowStateKind.Maximized);

    /// <summary>
    /// Reflects the maximized MDI child on the parent chrome: the combined
    /// "Program Manager - [Group]" title and the menu-bar system/restore boxes.
    /// </summary>
    private void UpdateMdiChrome()
    {
        var maximized = MaximizedEntry;
        var title = maximized is null ? Strings.AppTitle : $"{Strings.AppTitle} - [{maximized.Vm.Name}]";
        Title = title;
        TitleText.Text = title;
        MdiSystemBox.IsVisible = maximized is not null;
        MdiRestoreBox.IsVisible = maximized is not null;
    }

    private void MinimizeGroup(GroupEntry entry)
    {
        if (entry.State != WindowStateKind.Minimized)
        {
            if (entry.State == WindowStateKind.Normal)
            {
                entry.NormalBounds = GetWindowBounds(entry);
            }

            // Minimizing the maximized child keeps the workspace in maximized
            // mode: the next group opened comes up maximized, like real MDI.
            ShowGroupIcon(entry);
            ArrangeMinimizedIcons();
            UpdateMdiChrome();
            if (_active == entry)
            {
                ActivateGroup(entry);
            }
        }
    }

    /// <summary>Opens a minimized group, honoring the sticky maximized MDI mode.</summary>
    private void OpenGroup(GroupEntry entry)
    {
        if (_mdiMaximized && entry.State == WindowStateKind.Minimized)
        {
            MaximizeGroup(entry);
        }
        else
        {
            RestoreGroup(entry);
        }
    }

    private void RestoreGroup(GroupEntry entry)
    {
        _mdiMaximized = false;
        entry.State = WindowStateKind.Normal;
        ShowGroupWindow(entry);
        ArrangeMinimizedIcons();
        UpdateMdiChrome();
        ActivateGroup(entry);
    }

    private void MaximizeGroup(GroupEntry entry)
    {
        if (entry.State == WindowStateKind.Normal)
        {
            entry.NormalBounds = GetWindowBounds(entry);
        }

        var previous = MaximizedEntry;
        if (previous is not null && previous != entry)
        {
            previous.State = WindowStateKind.Normal;
            previous.Window.SetMaximizedLook(false);
        }

        entry.State = WindowStateKind.Maximized;
        ShowGroupWindow(entry);
        entry.State = WindowStateKind.Maximized;
        ApplyMaximizedBounds(entry);
        ArrangeMinimizedIcons();
        _mdiMaximized = true;
        UpdateMdiChrome();
        ActivateGroup(entry);
    }

    private void ApplyMaximizedBounds(GroupEntry entry)
    {
        entry.Window.SetMaximizedLook(true);
        Canvas.SetLeft(entry.Window, 0);
        Canvas.SetTop(entry.Window, 0);
        entry.Window.Width = Math.Max(100, MdiCanvas.Bounds.Width);
        entry.Window.Height = Math.Max(60, MdiCanvas.Bounds.Height);
    }

    private void OnWorkspaceResized()
    {
        foreach (var entry in _groups.Where(g => g.State == WindowStateKind.Maximized))
        {
            ApplyMaximizedBounds(entry);
        }

        ArrangeMinimizedIcons();
    }

    private void CascadeGroups()
    {
        var visible = _groups.Where(g => g.State != WindowStateKind.Minimized).ToList();
        if (visible.Count == 0)
        {
            return;
        }

        _mdiMaximized = false;
        UpdateMdiChrome();

        var width = Math.Clamp(MdiCanvas.Bounds.Width * 0.55, 220, 420);
        var height = Math.Clamp(MdiCanvas.Bounds.Height * 0.5, 150, 300);
        for (var i = 0; i < visible.Count; i++)
        {
            var entry = visible[i];
            entry.State = WindowStateKind.Normal;
            entry.NormalBounds = new Rect(i * 24, i * 22, width, height);
            ShowGroupWindow(entry);
        }

        if (_active is { } active && active.State != WindowStateKind.Minimized)
        {
            ActivateGroup(active);
        }
    }

    private void TileGroups()
    {
        var visible = _groups.Where(g => g.State != WindowStateKind.Minimized).ToList();
        if (visible.Count == 0)
        {
            return;
        }

        _mdiMaximized = false;
        UpdateMdiChrome();

        var columns = (int)Math.Ceiling(Math.Sqrt(visible.Count));
        var rows = (int)Math.Ceiling(visible.Count / (double)columns);
        // Leave the icon strip at the bottom visible, like the original Tile.
        var hasIcons = _groups.Any(g => g.State == WindowStateKind.Minimized);
        var availableHeight = Math.Max(80, MdiCanvas.Bounds.Height - (hasIcons ? 68 : 0));
        var width = Math.Max(120, MdiCanvas.Bounds.Width / columns);
        var height = Math.Max(80, availableHeight / rows);
        for (var i = 0; i < visible.Count; i++)
        {
            var entry = visible[i];
            entry.State = WindowStateKind.Normal;
            entry.NormalBounds = new Rect((i % columns) * width, (i / columns) * height, width, height);
            ShowGroupWindow(entry);
        }

        if (_active is { } active && active.State != WindowStateKind.Minimized)
        {
            ActivateGroup(active);
        }
    }

    private void ArrangeMinimizedIcons()
    {
        var icons = _groups.Where(g => g.Icon is not null).Select(g => g.Icon!).ToList();
        if (icons.Count == 0)
        {
            return;
        }

        var hostWidth = Math.Max(160, MdiCanvas.Bounds.Width);
        var hostHeight = Math.Max(120, MdiCanvas.Bounds.Height);
        var perRow = Math.Max(1, (int)((hostWidth - 8) / 78));
        for (var i = 0; i < icons.Count; i++)
        {
            Canvas.SetLeft(icons[i], 6 + (i % perRow) * 78);
            Canvas.SetTop(icons[i], hostHeight - 62 - (i / perRow) * 64);
        }
    }

    private void ArrangeActiveIcons()
    {
        // Like the original: arranges the active group's icons, or the icon
        // strip when a minimized group is the active one.
        if (_active is { State: not WindowStateKind.Minimized } active)
        {
            active.Window.ArrangeIcons();
        }
        else
        {
            ArrangeMinimizedIcons();
        }
    }

    private void ActivateNextGroup()
    {
        if (_groups.Count == 0)
        {
            return;
        }

        var index = _active is null ? 0 : (_groups.IndexOf(_active) + 1) % _groups.Count;
        ActivateGroup(_groups[index]);
    }

    private void MinimizeActiveGroup()
    {
        if (_active is { } active)
        {
            MinimizeGroup(active);
        }
    }

    // ---------------------------------------------------------- group editing

    private void SaveGroups() => GroupsStore.Save(_groups.Select(g => g.Vm.ToModel()));

    private GroupEntry? FindGroup(string name) =>
        _groups.FirstOrDefault(g => string.Equals(g.Vm.Name, name, StringComparison.OrdinalIgnoreCase));

    private ItemViewModel? SelectedItemOfActiveWindow =>
        _active is { State: not WindowStateKind.Minimized } active ? active.Window.SelectedItem : null;

    private void UpdateFileMenuState()
    {
        var activeIsIcon = _active is { State: WindowStateKind.Minimized };
        var hasSelectedItem = SelectedItemOfActiveWindow is not null;
        MenuOpen.IsEnabled = hasSelectedItem || activeIsIcon;
        MenuMove.IsEnabled = hasSelectedItem && _groups.Count > 1;
        MenuCopy.IsEnabled = hasSelectedItem && _groups.Count > 1;
        MenuDelete.IsEnabled = hasSelectedItem || activeIsIcon;
        MenuProperties.IsEnabled = _active is not null;
    }

    private Task ShowError(string message) =>
        new MessageDialog(Strings.AppTitle, message).ShowDialog(this);

    private async void OnMenuNewClicked(object? sender, RoutedEventArgs e)
    {
        var choice = new NewObjectDialog();
        if (!await choice.ShowDialog<bool>(this))
        {
            return;
        }

        if (choice.CreateGroup)
        {
            await NewGroup();
        }
        else
        {
            await NewItem();
        }
    }

    private async Task NewGroup()
    {
        var dialog = new GroupPropertiesDialog("");
        if (!await dialog.ShowDialog<bool>(this))
        {
            return;
        }

        if (FindGroup(dialog.GroupName) is not null)
        {
            await ShowError(Strings.ErrGroupExists(dialog.GroupName));
            return;
        }

        var entry = CreateEntry(new GroupViewModel(new ProgramGroup(dialog.GroupName, [])));
        entry.State = WindowStateKind.Normal;
        entry.NormalBounds = new Rect(
            16 + (_groups.Count % 6) * 24,
            12 + (_groups.Count % 6) * 22,
            Math.Clamp(MdiCanvas.Bounds.Width * 0.55, 220, 360),
            Math.Clamp(MdiCanvas.Bounds.Height * 0.5, 150, 220));
        ShowGroupWindow(entry);
        ActivateGroup(entry);
        SaveGroups();
    }

    private async Task NewItem()
    {
        if (_active is not { } active)
        {
            return;
        }

        var dialog = new ItemPropertiesDialog("", "");
        if (!await dialog.ShowDialog<bool>(this))
        {
            return;
        }

        if (active.Vm.Items.Any(i => string.Equals(i.Name, dialog.ItemName, StringComparison.OrdinalIgnoreCase)))
        {
            await ShowError(Strings.ErrItemExists(dialog.ItemName, active.Vm.Name));
            return;
        }

        var item = new ItemViewModel(new ProgramItem(dialog.ItemName, dialog.ItemPath));
        active.Vm.Items.Add(item);
        if (active.State == WindowStateKind.Minimized)
        {
            RestoreGroup(active);
        }

        SaveGroups();
        LoadIconsAsync([item]);
    }

    private async void OnMenuPropertiesClicked(object? sender, RoutedEventArgs e) => await EditProperties();

    private async Task EditProperties()
    {
        if (_active is not { } active)
        {
            return;
        }

        if (SelectedItemOfActiveWindow is { } item)
        {
            var dialog = new ItemPropertiesDialog(item.Name, item.Path);
            if (!await dialog.ShowDialog<bool>(this))
            {
                return;
            }

            if (!string.Equals(dialog.ItemName, item.Name, StringComparison.OrdinalIgnoreCase) &&
                active.Vm.Items.Any(i => string.Equals(i.Name, dialog.ItemName, StringComparison.OrdinalIgnoreCase)))
            {
                await ShowError(Strings.ErrItemExists(dialog.ItemName, active.Vm.Name));
                return;
            }

            var pathChanged = !string.Equals(item.Path, dialog.ItemPath, StringComparison.OrdinalIgnoreCase);
            item.Name = dialog.ItemName;
            item.Path = dialog.ItemPath;
            if (pathChanged)
            {
                item.Icon = null;
                LoadIconsAsync([item]);
            }

            SaveGroups();
        }
        else
        {
            var dialog = new GroupPropertiesDialog(active.Vm.Name);
            if (!await dialog.ShowDialog<bool>(this) || string.Equals(dialog.GroupName, active.Vm.Name, StringComparison.Ordinal))
            {
                return;
            }

            if (!string.Equals(dialog.GroupName, active.Vm.Name, StringComparison.OrdinalIgnoreCase) &&
                FindGroup(dialog.GroupName) is not null)
            {
                await ShowError(Strings.ErrGroupExists(dialog.GroupName));
                return;
            }

            active.Vm.Name = dialog.GroupName;
            active.Icon?.UpdateTitle(dialog.GroupName);
            UpdateMdiChrome();
            SaveGroups();
        }
    }

    private async void OnMenuDeleteClicked(object? sender, RoutedEventArgs e) => await DeleteSelection();

    private async Task DeleteSelection()
    {
        if (_active is not { } active)
        {
            return;
        }

        if (SelectedItemOfActiveWindow is { } item)
        {
            var confirm = new ConfirmDialog(Strings.DeleteTitle, Strings.ConfirmDeleteItem(item.Name));
            if (await confirm.ShowDialog<bool>(this))
            {
                active.Vm.Items.Remove(item);
                SaveGroups();
            }
        }
        else if (active.State == WindowStateKind.Minimized)
        {
            // Like the original, a whole group can only be deleted while its icon
            // (not an open window) is the active selection.
            var confirm = new ConfirmDialog(Strings.DeleteTitle, Strings.ConfirmDeleteGroup(active.Vm.Name));
            if (await confirm.ShowDialog<bool>(this))
            {
                RemoveGroupEntry(active);
                SaveGroups();
            }
        }
    }

    private void RemoveGroupEntry(GroupEntry entry)
    {
        MdiCanvas.Children.Remove(entry.Window);
        if (entry.Icon is not null)
        {
            MdiCanvas.Children.Remove(entry.Icon);
        }

        _groups.Remove(entry);
        ArrangeMinimizedIcons();
        UpdateMdiChrome();
        if (_active == entry)
        {
            _active = null;
            var next = _groups.FirstOrDefault(g => g.State != WindowStateKind.Minimized) ?? _groups.FirstOrDefault();
            if (next is not null)
            {
                ActivateGroup(next);
            }
        }
    }

    // ------------------------------------------------ drag between groups

    /// <summary>
    /// The icon the pointer carries while a drag is out over the workspace. The
    /// original drags the icon itself, so this is the plain 32x32 art.
    /// </summary>
    private void ShowDragGhost(IconDragEventArgs args)
    {
        if (_dragGhost is null)
        {
            _dragGhost = new Panel
            {
                Width = 32,
                Height = 32,
                IsHitTestVisible = false,
                ZIndex = int.MaxValue,
                Children =
                {
                    new GroupIconArt { Width = 32, Height = 32, IsVisible = !args.Item.HasIcon },
                    new Image { Width = 32, Height = 32, Source = args.Item.Icon },
                },
            };
            MdiCanvas.Children.Add(_dragGhost);
        }

        Canvas.SetLeft(_dragGhost, args.Point.X - 16);
        Canvas.SetTop(_dragGhost, args.Point.Y - 16);
    }

    private void HideDragGhost()
    {
        if (_dragGhost is not null)
        {
            MdiCanvas.Children.Remove(_dragGhost);
            _dragGhost = null;
        }
    }

    private async void OnIconDroppedOut(GroupEntry source, IconDragEventArgs args)
    {
        HideDragGhost();
        var found = FindDropTarget(source, args.Point);
        if (found is not { } target)
        {
            return;
        }

        if (target.Vm.Items.Any(i => string.Equals(i.Name, args.Item.Name, StringComparison.OrdinalIgnoreCase)))
        {
            await ShowError(Strings.ErrItemExists(args.Item.Name, target.Vm.Name));
            return;
        }

        var item = args.Item;
        if (args.IsCopy)
        {
            item = new ItemViewModel(new ProgramItem(item.Name, item.Path)) { Icon = item.Icon };
        }
        else
        {
            source.Vm.Items.Remove(item);
        }

        item.ClearPosition();
        target.Vm.Items.Add(item);
        target.Window.PlaceDroppedIcon(item, args.Point);
        SaveGroups();
    }

    /// <summary>
    /// The group under the drop point: the topmost group window, or a minimized
    /// group icon. Dropping anywhere else leaves the item where it was.
    /// </summary>
    private GroupEntry? FindDropTarget(GroupEntry source, Point point)
    {
        foreach (var child in ChildrenTopToBottom())
        {
            if (!child.IsVisible || !child.Bounds.Contains(point))
            {
                continue;
            }

            var entry = _groups.FirstOrDefault(g => ReferenceEquals(g.Window, child) || ReferenceEquals(g.Icon, child));
            if (entry is null)
            {
                continue;
            }

            // A hit on the source itself blocks whatever is underneath it.
            return entry == source ? null : entry;
        }

        return null;
    }

    /// <summary>Workspace children in painting order, topmost first (see ZIndex).</summary>
    private IEnumerable<Control> ChildrenTopToBottom() =>
        MdiCanvas.Children
            .Select((child, index) => (child, index))
            .OrderByDescending(c => c.child.ZIndex)
            .ThenByDescending(c => c.index)
            .Select(c => c.child);

    private async void OnMenuMoveClicked(object? sender, RoutedEventArgs e) => await MoveOrCopySelectedItem(isCopy: false);

    private async void OnMenuCopyClicked(object? sender, RoutedEventArgs e) => await MoveOrCopySelectedItem(isCopy: true);

    private async Task MoveOrCopySelectedItem(bool isCopy)
    {
        if (_active is not { } active || SelectedItemOfActiveWindow is not { } item)
        {
            return;
        }

        var targets = _groups.Where(g => g != active).Select(g => g.Vm.Name).ToList();
        if (targets.Count == 0)
        {
            return;
        }

        var dialog = new MoveCopyDialog(item.Name, active.Vm.Name, targets, isCopy);
        if (!await dialog.ShowDialog<bool>(this) ||
            dialog.SelectedGroup is not { } targetName ||
            FindGroup(targetName) is not { } target)
        {
            return;
        }

        if (target.Vm.Items.Any(i => string.Equals(i.Name, item.Name, StringComparison.OrdinalIgnoreCase)))
        {
            await ShowError(Strings.ErrItemExists(item.Name, target.Vm.Name));
            return;
        }

        if (isCopy)
        {
            var copy = new ItemViewModel(new ProgramItem(item.Name, item.Path)) { Icon = item.Icon };
            target.Vm.Items.Add(copy);
        }
        else
        {
            active.Vm.Items.Remove(item);
            // The moved icon takes a fresh grid cell in its new group.
            item.ClearPosition();
            target.Vm.Items.Add(item);
        }

        SaveGroups();
    }

    private async void OnMenuRefreshClicked(object? sender, RoutedEventArgs e) => await RefreshFromStartMenu();

    private async Task RefreshFromStartMenu()
    {
        var roots = StartMenuRoots;
        var mainGroupName = Strings.MainGroupName;
        var scanned = await Task.Run(() => new StartMenuScanner(AppEntrySources.ForCurrentPlatform()).Scan(roots, mainGroupName));
        var result = StartMenuMerge.Merge(_groups.Select(g => g.Vm.ToModel()).ToList(), scanned);
        if (result.AddedGroups == 0 && result.AddedItems == 0)
        {
            await new MessageDialog(Strings.AppTitle, Strings.RefreshNothing).ShowDialog(this);
            return;
        }

        var newItems = new List<ItemViewModel>();
        foreach (var merged in result.Groups)
        {
            if (FindGroup(merged.Name) is { } entry)
            {
                // Merge appends new items at the end, so anything past the current
                // count is an addition.
                for (var i = entry.Vm.Items.Count; i < merged.Items.Count; i++)
                {
                    var item = new ItemViewModel(merged.Items[i]);
                    entry.Vm.Items.Add(item);
                    newItems.Add(item);
                }
            }
            else
            {
                var created = CreateEntry(new GroupViewModel(merged));
                created.NormalBounds = new Rect(
                    24, 20,
                    Math.Clamp(MdiCanvas.Bounds.Width * 0.55, 220, 360),
                    Math.Clamp(MdiCanvas.Bounds.Height * 0.5, 150, 220));
                ShowGroupIcon(created);
                newItems.AddRange(created.Vm.Items);
            }
        }

        ArrangeMinimizedIcons();
        SaveGroups();
        LoadIconsAsync(newItems);
        await new MessageDialog(Strings.AppTitle, Strings.RefreshResult(result.AddedGroups, result.AddedItems)).ShowDialog(this);
    }

    // ---------------------------------------------------------------- actions

    private void OpenSelected()
    {
        if (_active is not { } active)
        {
            return;
        }

        if (active.State == WindowStateKind.Minimized)
        {
            OpenGroup(active);
            return;
        }

        if (active.Window.SelectedItem is { } item)
        {
            _ = LaunchItem(item);
        }
    }

    private async Task LaunchItem(ItemViewModel item)
    {
        try
        {
            ShellLauncher.Launch(item.Path);
            if (_settings.MinimizeOnUse)
            {
                WindowState = WindowState.Minimized;
            }
        }
        catch (Exception)
        {
            await ShowLaunchError(item.Path);
        }
    }

    private Task ShowLaunchError(string path) =>
        new MessageDialog(Strings.AppTitle, Strings.LaunchError(path)).ShowDialog(this);

    // ------------------------------------------------------------------- exit

    private async void RequestExit()
    {
        var confirmed = await new ExitDialog().ShowDialog<bool>(this);
        if (confirmed)
        {
            _exitConfirmed = true;
            Close();
        }
    }

    private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        if (_screenshotPath is null && !_exitConfirmed)
        {
            e.Cancel = true;
            RequestExit();
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (_screenshotPath is null && !_exitConfirmed)
        {
            e.Cancel = true;
            RequestExit();
            return;
        }

        if (_settings.SaveSettingsOnExit && _screenshotPath is null)
        {
            CollectSettings();
            SettingsStore.Save(_settings);
        }

        base.OnClosing(e);
    }

    private void CollectSettings()
    {
        var bounds = _isCustomMaximized && _normalWindowBounds is { } normal
            ? normal
            : new Rect(Position.X, Position.Y, Width, Height);
        _settings.MainWindow = new WindowPlacement(
            (int)bounds.X,
            (int)bounds.Y,
            (int)bounds.Width,
            (int)bounds.Height,
            _isCustomMaximized ? WindowStateKind.Maximized : WindowStateKind.Normal);

        _settings.ActiveGroup = _active?.Vm.Name;

        _settings.GroupOrder.Clear();
        // Persist the MDI z-order: open windows bottom to top, icons afterwards.
        foreach (var child in ChildrenTopToBottom().Reverse())
        {
            var entry = _groups.FirstOrDefault(g => g.Window == child);
            if (entry is not null)
            {
                _settings.GroupOrder.Add(entry.Vm.Name);
            }
        }

        foreach (var entry in _groups.Where(g => g.State == WindowStateKind.Minimized))
        {
            _settings.GroupOrder.Add(entry.Vm.Name);
        }

        _settings.GroupWindows.Clear();
        foreach (var entry in _groups)
        {
            var rect = entry.State == WindowStateKind.Normal ? GetWindowBounds(entry) : entry.NormalBounds;
            _settings.GroupWindows[entry.Vm.Name] = new WindowPlacement(
                (int)rect.X,
                (int)rect.Y,
                (int)rect.Width,
                (int)rect.Height,
                entry.State);
        }

        _settings.IconPositions.Clear();
        foreach (var entry in _groups)
        {
            var icons = new Dictionary<string, IconPosition>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in entry.Vm.Items.Where(i => i.IsPlaced))
            {
                icons[item.Name] = new IconPosition((int)item.IconX, (int)item.IconY);
            }

            if (icons.Count > 0)
            {
                _settings.IconPositions[entry.Vm.Name] = icons;
            }
        }
    }

    // -------------------------------------------------------------- screenshot

    private Window? _sceneDialog;

    private void PrepareScreenshotScene()
    {
        switch (_screenshotScene)
        {
            case "file-menu":
                if (MainMenu.Items[0] is MenuItem file)
                {
                    MainMenu.Focus();
                    file.IsSubMenuOpen = true;
                }

                break;
            case "options-menu":
                if (MainMenu.Items[1] is MenuItem options)
                {
                    MainMenu.Focus();
                    options.IsSubMenuOpen = true;
                }

                break;
            case "window-menu":
                if (MainMenu.Items[2] is MenuItem windowMenu)
                {
                    MainMenu.Focus();
                    RebuildWindowMenu();
                    windowMenu.IsSubMenuOpen = true;
                }

                break;
            case "run":
                _sceneDialog = new RunDialog();
                _ = _sceneDialog.ShowDialog(this);
                break;
            case "new-object":
                _sceneDialog = new NewObjectDialog();
                _ = _sceneDialog.ShowDialog(this);
                break;
            case "item-props":
                _sceneDialog = OperatingSystem.IsMacOS()
                    ? new ItemPropertiesDialog("TextEdit", "/System/Applications/TextEdit.app")
                    : new ItemPropertiesDialog("Notepad", @"C:\Windows\notepad.exe");
                _ = _sceneDialog.ShowDialog(this);
                break;
            case "move":
                _sceneDialog = new MoveCopyDialog("Notepad", "Main", ["Accessories", "Games", "StartUp"], isCopy: false);
                _ = _sceneDialog.ShowDialog(this);
                break;
            case "exit":
                _sceneDialog = new ExitDialog();
                _ = _sceneDialog.ShowDialog(this);
                break;
            case "about":
                _sceneDialog = new AboutDialog();
                _ = _sceneDialog.ShowDialog(this);
                break;
            case "small":
                if (_groups.FirstOrDefault(g => g.State != WindowStateKind.Minimized) is { } entry)
                {
                    entry.NormalBounds = new Rect(10, 10, 300, 180);
                    ShowGroupWindow(entry);
                }

                break;
            case "maximized":
                if (_groups.FirstOrDefault(g => g.State != WindowStateKind.Minimized) is { } toMax)
                {
                    MaximizeGroup(toMax);
                }

                break;
            case "free-icons":
                // Auto Arrange off with hand-scattered icons, to exercise free
                // placement and the scroll bars that appear around it.
                MenuAutoArrange.IsChecked = false;
                OnOptionToggled(null, null!);
                if (_groups.FirstOrDefault(g => g.State != WindowStateKind.Minimized) is { } free)
                {
                    for (var i = 0; i < free.Vm.Items.Count; i++)
                    {
                        var item = free.Vm.Items[i];
                        item.SetPosition(10 + i * 53 % 260, 8 + i * 47 % 150);
                    }
                }

                break;
        }
    }

    private void CaptureScreenshotAndExit()
    {
        try
        {
            if (_sceneDialog is { Content: Visual dialogRoot } dialog)
            {
                var dialogSize = new PixelSize(
                    Math.Max(1, (int)dialog.Bounds.Width),
                    Math.Max(1, (int)dialog.Bounds.Height));
                var dialogBitmap = new RenderTargetBitmap(dialogSize, new Vector(96, 96));
                dialogBitmap.Render(dialogRoot);
                dialogBitmap.Save(_screenshotPath!);
            }
            else
            {
                var size = new PixelSize((int)Bounds.Width, (int)Bounds.Height);
                var bitmap = new RenderTargetBitmap(size, new Vector(96, 96));
                bitmap.Render(this);
                bitmap.Save(_screenshotPath!);
            }
        }
        finally
        {
            _exitConfirmed = true;
            _sceneDialog?.Close();
            Close();
        }
    }
}

internal sealed class RelayCommand(Action action) : ICommand
{
    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => action();

    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }
}