| `test/ReProgman.Model.Tests` | xUnit tests for the model (xunit.v3 / Microsoft.Testing.Platform) |
| `tools/ReProgman.IconExport` | Developer tool, never shipped: renders the committed icon assets from the app's own artwork (see "The application icon") |
| `tools/ReProgman.IconExport` | Developer tool, never shipped: renders the committed icon assets from the app's own artwork (see "The application icon") |
# ReProgman Specification

A desktop application that reproduces the look and feel of the Windows 3.1
Program Manager (progman.exe), built on .NET 10 + Avalonia 11. The name means a
re-implementation of the original (Progman); the window title stays
"Program Manager" / "プログラム マネージャ" exactly as in the original.
It runs on Windows and macOS, and **keeps the Windows 3.1 look and behaviour on
both** (no global menu bar, no traffic light buttons, no other macOS-native
conventions).

## Project layout

| Project | Role |
|---|---|
| `src/ReProgman.Model` | UI-independent, OS-independent logic (INI reading/writing, settings, program folder scanning, .icns / plist parsing, storage location resolution). net10.0 |
| `src/ReProgman` | The Avalonia UI itself (net10.0, shared between Windows and macOS). The executable is named `ReProgman` |
| `test/ReProgman.Model.Tests` | xUnit tests for the model (xunit.v3 / Microsoft.Testing.Platform) |
| `tools/ReProgman.IconExport` | Developer tool, never shipped: renders the committed icon assets from the app's own artwork (see "The application icon") |

## Functional specification

### Showing groups
- The groups and items in `Groups.ini`, in the settings folder (see "Saving
  settings" below), are the source of truth. The program folders are scanned only
  on the first run to create `Groups.ini`; every edit after that is written back
  to that file immediately. **Nothing is ever written back to where the entries
  were imported from.**
- `Groups.ini` format: `[Groups] GroupN=<name>` (display order) and
  `[Group.<name>] ItemN=<display name>|<path>`. Both the display name and the
  path may contain `|`, so a `|` in the path is doubled to `||` when written, and
  reading splits at "the first `|` that is followed only by evenly paired pipes".
  For values without doubling this gives the same result as the Windows-only
  "split at the last `|`" rule, so existing files still read correctly.
- Program folders that are scanned:

  | OS | Roots (earlier entries win) |
  |---|---|
  | Windows | The Start Menu `Programs` folders (user and common) |
  | macOS | `~/Applications`, `/Applications`, `/System/Applications` |

- Each top-level folder becomes one group, shown in an MDI child window.
- Entries directly under a root go into the **Main** group, following the
  original's default group.
- Nested subfolders are flattened into their top-level group (groups in the
  original have no hierarchy).
- Groups with the same name in the user and common folders are merged; for items
  with the same name the user's copy wins.
- What counts as an entry differs per platform (`IAppEntrySource`). Grouping,
  flattening, merging and sorting are shared code; only the enumeration is
  swapped out.

  | OS | Entries | Excluded |
  |---|---|---|
  | Windows | `.lnk` / `.url` files | `desktop.ini`, hidden and system files |
  | macOS | `.app` bundles (directories) | Names starting with `.`. Bundles are not descended into |

  On macOS an `.app` is itself a directory, so an `.app` directly under a root
  becomes an item in the Main group rather than a group of its own. The display
  name is the bundle name without `.app` (no localized `CFBundleDisplayName`
  resolution).
- Groups are sorted alphabetically with Main first; items are sorted
  alphabetically.
- Icons are loaded at 32x32, one after another on a dedicated background thread.
  Windows asks the shell through `SHGetFileInfo` (which needs STA) for the index
  into the system image list and pulls the icon out of that list, because asking
  for the icon of a `.lnk` directly composites the shortcut arrow into it and
  Program Manager items never had one; macOS decodes
  the `.icns` that `CFBundleIconFile` in `Contents/Info.plist` points at
  (`IcnsDecoder`: PNG chunks are used as they are, the older RLE 24-bit and ARGB
  chunks are expanded to BGRA, and the representation closest to 32px wins).
  When no icon can be obtained, the hand-drawn group icon art is used instead.

### Launching applications
- Double-click an item, or select it and press Enter / File → Open. Windows hands
  the shortcut to `UseShellExecute` as it is; macOS passes `.app` bundles and
  documents to `open` (the same as double-clicking in Finder) and starts
  executable files directly.
- File → Run...: a dialog with a command line box, a Run Minimized check box and
  Browse...
- Options → Minimize on Use minimizes the main window when something is launched.
- Run Minimized has no macOS equivalent, so the check box stays but is ignored.
- A failed launch shows a Windows 3.1 style message dialog.

### Editing groups and items (File menu)
- **New...**: like the original, a dialog with radio buttons for "Program Group"
  and "Program Item". A group only takes a description (its name); an item takes
  a description and a command line (with Browse...). The item is added to the
  active group and its icon is fetched from the shell.
- **Move... (F7) / Copy... (F8)**: moves or copies the selected item to another
  group, chosen from a list box. The moved icon lands in a free cell of the
  target group.
- **Drag and drop**: dragging an item's icon out of its window makes the icon
  follow the cursor; dropping it on another group window, or on a minimized group
  icon, moves the item there. Holding Ctrl copies instead (as in the original).
  Dropping on empty workspace puts the icon back where it was. In the target, the
  icon lands under the cursor when Auto Arrange is off, or in a free grid cell
  when it is on. An item of the same name in the target is reported as an error.
- **Delete (Del)**: deletes the selected item, or the active group, after a
  Yes/No confirmation. As in the original, a group can only be deleted while a
  minimized (iconized) group is the active one.
- **Properties... (Alt+Enter)**: edits the description and command line of the
  selected item, or the name of the active group when no item is selected.
  Changing the path re-fetches the icon.
- Group names are unique within the application; item names are unique within
  their group (duplicates are reported as an error).
- The File menu items are enabled and disabled according to the current
  selection.
- Edits are saved to `Groups.ini` immediately, independently of Save Settings on
  Exit.

### Importing from the program folders (File → Refresh from ...)
- The menu wording follows the platform (Windows: Refresh from Start Menu,
  macOS: Refresh from Applications).
- The program folders are scanned again, and only the groups and items that are
  missing from `Groups.ini` (matched by name, case-insensitively) are added; the
  counts are then reported.
- What the user has edited or deleted is not overwritten: user-created groups
  stay, and edited paths are kept. Imported entries that were deleted do come
  back on the next import, though.
- After switching the display language the default group name differs
  (`Main` / `メイン`), so an import can produce both groups (known limitation).

### Icon placement inside a group, and Auto Arrange
- Icons are placed freely on a grid of 74x62 cells and can be moved by dragging
  (a 5px threshold separates a drag from a click or double-click).
- With Options → Auto Arrange **on**: icons snap into the grid in item order
  whenever the window is resized or an icon is dropped. The drop position decides
  the insertion index, so the item order changes too (the same behaviour as the
  original).
- With it **off**: icons stay where they are put, overlaps and gaps included. An
  icon placed outside the visible area brings up the scroll bars. A new icon with
  no position yet goes into the first free cell in row-major order.
- Window → Arrange Icons arranges the icons of the active group window, or the
  row of group icons along the bottom when the active group is minimized.
- Icon positions are saved in ReProgman.ini under `[Icons.<group name>]` as
  `IconN=x,y,<item name>` (the name comes last because it may contain commas).
- The arrow keys move the selection to the nearest icon in that direction, which
  works for a free layout as well as for the grid; at the ends of a row left and
  right carry on into the neighbouring one. They are handled by the frame window
  and routed to the active group, so the selection moves wherever the focus
  happens to sit, and an open menu still takes them first (it marks them handled).
- The icon list has to declare `Focusable="True"` in its ControlTheme: the stock
  themes set it themselves, and without it the list cannot take the keyboard
  focus, which would leave the menu bar holding it from startup and make the
  arrow keys drive the menu instead.

### MDI
- Group windows live on the main window's client area (a Canvas).
- A group window can be moved, resized in 8 directions, minimized, maximized and
  restored. Closing it means minimizing it (the same as a group in the original).
- Maximizing behaves like the original MDI:
  - The child's title bar and frame disappear and its contents fill the
    workspace.
  - The parent title bar becomes "Program Manager - [group name]", and the parent
    menu bar grows the child's system box at its left end (short bar, flat) and a
    restore button at its right end (▲▼, with the same bevel as a caption button,
    pressed in when clicked). Both are as tall as the menu bar. The system box
    opens the child's system menu (double-clicking closes it, i.e. iconizes it).
  - The maximized state follows the active child: activating another group
    maximizes that one, and a group opened from its icon opens maximized too.
    An explicit Restore, or Cascade / Tile, leaves the mode.
- A minimized group becomes an icon along the bottom of the workspace. A single
  click opens its system menu, a double-click restores it.
- The stacking order is managed through `ZIndex`, because reordering the canvas
  children re-parents a window in the middle of a click and the rest of that
  input (a drag, for instance) is lost. Minimized icons keep the default 0 and
  therefore always stay below the windows.
- When the application loses focus, not only the main window's title bar but also
  **the active child window's title bar turns to the inactive colours (black on
  white)**. The selection inside the group (the inverted icon label) stays as it
  is.
- Window menu: Cascade (Shift+F5), Tile (Shift+F4), Arrange Icons and the list of
  groups (with a check mark on the active one). Ctrl+F6 / Ctrl+Tab activate the
  next group.
- The workspace (the MDI client area) has a white background.
- A single click on a system box opens the system menu, a double-click closes the
  window (main window = exit confirmation, group = iconize, dialog = close as if
  cancelled). The box itself has to stay clickable while the popup is open,
  otherwise light dismiss swallows the second press of the double-click.

### Look and feel (the Windows 3.1 "Windows Default" scheme)
- Every control is drawn by a ControlTheme of our own, on top of the Avalonia
  Simple theme.
- Window chrome: `SystemDecorations=None`, and the sizing frame (1px black + gray
  band + 1px black, with corner notches), the navy title bar, the system box (the
  "space bar" glyph) and the ▼▲ minimize/maximize buttons are all drawn by hand.
  An inactive title bar is black on white. The sizing frame disappears while
  maximized.
- Dragging the sizing frame resizes the window in 8 directions, with the edges
  that were not grabbed staying put and `MinWidth`/`MinHeight` pinning the
  grabbed edge. The main window hands the drag to the window manager
  (`BeginResizeDrag`) on Windows, and runs it itself on macOS, where Avalonia's
  backend implements that call as an empty method. The arithmetic behind both
  paths, and behind the MDI children, is `WindowResize.Drag` in the Model.
- The macOS drag reads the pointer from the window server (`MacPointer`) instead
  of taking it from the input event, and never reads the window's own geometry
  back while dragging. Event positions are relative to the window, and a west or
  north drag moves that window, so measuring against them makes the drag an
  undamped feedback loop that visibly shakes. The MDI children are immune to this
  because their pointer positions are relative to the workspace canvas, which
  does not move when a child is resized.
- Measured dimensions (compared pixel by pixel against toastytech's 640x480
  screenshots of the original):
  - The sizing frame is black 1 + gray band 2 + black 1 = 4px. The corner notches
    are 22px from the edge (frame 4 + caption 18). The same for the main window
    and the MDI children.
  - The caption face is 18px for the main window, the children and the menu bar
    (plus a 1px black separator line).
  - The face of a system box or caption button is 18px wide (plus a 1px black
    separator line).
- The system box glyph: a BarWidth x 3 black box around a 1px-high white face,
  with a 1px gray shadow to the right and below (+1,+1). BarWidth is 13 for the
  main (top-level) window and 7 for an MDI child — the shorter bar is how you
  tell a child from the main window.
- Caption button arrows: a 4-row arrow head (widths 1,3,5,7). ▼ sits at the top
  of the face +7 and ▲ at +6 (▲ is 1px higher in the original). The restore mark
  stacks ▲▼ with a 1px gap. Scroll bar arrows add a 3px-wide shaft to the head
  (7x7).
- Button bevels are asymmetric, 1px white and 2px shadow (caption buttons, the
  scroll bar arrow buttons and thumb). Push buttons use 2 and 2.
- Button content is centered, by centering the ContentPresenter itself
  (stretching it would leave the label left-aligned).
- Menus: white background, bold text, navy background with white text for the
  selection, gray for disabled items, and accelerator underlines shown at all
  times. Shortcut text such as `Del` is hand-drawn text through the
  `MenuProps.GestureText` attached property.
- Scroll bars: 16px wide, arrow buttons with a black outline and a bevel, and a
  fixed size (16px) thumb. The track (the shaft) is the same flat gray as the
  buttons (measured on the original: `C0C0C0`; the dither pattern belongs to
  Windows 95 and later, not to 3.1).
- Buttons: a gray face, a 2px white/dark gray bevel and a black outline. The
  default button gets a second black outline. Pressing inverts the bevel and
  shifts the content by 1px. Check boxes use an X mark.
- Fonts: Avalonia (DirectWrite/Skia) cannot enumerate GDI raster fonts (.FON,
  MS Sans Serif and friends), so the raster font look comes from a TrueType
  approximation plus `TextRenderingMode=Alias` (no antialiasing). No fonts are
  bundled.
  - System / caption / menu / dialog text: `Arial, Helvetica, ...` 14px bold.
    Microsoft Sans Serif (micross.ttf) only has a Regular face, and the
    synthesized bold smears, so a Helvetica-style family with a real bold face is
    used instead (Arial on Windows, Arial or Helvetica on macOS). The original
    System font is a Helvetica derivative as well.
  - Icon labels: `Microsoft Sans Serif, Helvetica, ...` 11px (a real Regular
    face).
  - Japanese glyphs come from the `MS Gothic` / `Hiragino Kaku Gothic ProN`
    fallbacks. Shapes use `EdgeMode=Aliased` to reproduce the jagged edges.
  - RenderOptions are inherited per top level, so windows and dialogs get
    Alias/Aliased from a `Loaded` class handler on `TopLevel`. Popups (menus and
    flyouts) are separate top levels that the handler never reaches, so the roots
    of the popup content (the Border inside a menu's Popup and the
    `MenuFlyoutPresenter` template) set
    `RenderOptions.TextRenderingMode="Alias"` / `EdgeMode="Aliased"` themselves.
    Without that, only the menus are drawn with ClearType subpixel rendering and
    the glyph edges pick up colour.
- On Retina (2x) the logical-pixel drawing comes out as a crisp 2x enlargement.
  To get 1x drawing instead, set `NSHighResolutionCapable` in the `.app`'s
  `Info.plist` to `false`.
- DPI scaling: the PerMonitorV2 declaration makes the app follow the OS display
  scale, and the layout is left to Avalonia's DIP scaling. The hand-drawn chrome
  is aligned to device pixels through `PixelSnap` (App):
  - Glyph pixel art (the system box bar, the arrows, the information and group
    icons) is laid out in DIP space, which matches the original's pixel
    dimensions, and every edge is rounded to a device pixel when filled. The
    share of the control the art takes up is then the same at every scale (at
    150% the system box bar is 20px with 3 and 4px margins). A whole-number zoom
    (2x at 150%) is not used, because it enlarges the art beyond the control that
    holds it.
  - Both black lines of the sizing frame are the same thickness. Rounding the
    outer one separately with `round(scale)` would give 2px outside against 1px
    inside at 150%, so the outer one is derived the same way as the inner one,
    from "the padding boundary minus the band boundary". At 150% the frame is
    black 1 + gray 4 + black 1 = 6px.
  - Frame lines and bevels are drawn by rounding the boundaries to device pixels
    before filling, so a 1px line never wobbles between 1 and 2px along its
    length. `Win31Frame` snaps its Padding as well, which keeps the visible width
    of the inner black line equal on all four sides.
  - The `--screenshot` mode draws into a RenderTargetBitmap fixed at 96 DPI, so
    it sets `PixelSnap.Override = 1` to ignore the monitor's scale.
- Dialogs use the shared `DialogShell` chrome. The measured modal frame is 1px
  black + a 4px band in the active title colour (navy) + a 1px white line, with a
  white interior. There is no black line above the caption; the white line meets
  it. Esc cancels, and double-clicking the system box closes the dialog.
- The exit dialog is a message box: the information icon on the left (hand-drawn
  pixel art, a blue circle with a white i), the message on the right, and OK
  (the default, with focus) and Cancel centered below.

### Saving settings (portable)
- Settings go to `ReProgman.ini` in the same directory as the executable; the
  registry and similar stores are not used. `Groups.ini` sits in the same folder.
- On macOS the executable lives in `ReProgman.app/Contents/MacOS/`, so the base
  is three levels up, **the folder next to the `.app` bundle**. When that is not
  writable (App Translocation, a read-only volume), the app falls back to
  `~/Library/Application Support/ReProgman`. Writability is decided by actually
  writing a probe file.
- Windows stays portable-only as before, and silently gives up when it cannot
  write.
- What is saved: the position, size and state of the main window; the position,
  size and state of every group window; the Z order of the groups; the active
  group; and the three Options flags (Auto Arrange, Minimize on Use, Save
  Settings on Exit).
- Saving happens on exit when Save Settings on Exit is enabled.
- The format is INI, with `WindowPlacement` recorded as
  `x,y,width,height,state`. Sections: `[Settings]`, `[Groups]` (the Z order) and
  `[Group.<name>]`.
- On the very first run only the first group is opened; the rest become minimized
  icons.

### Exiting
- Alt+F4, Close from the system menu and File → Exit ReProgman... all show the
  "This will end your ReProgman session." dialog, and OK exits. The original said
  "Windows" here, since quitting Program Manager ended the session; this is a
  normal application, so it names itself.
- The macOS application menu and Cmd+Q are routed into the same confirmation
  dialog (by intercepting `ShutdownRequested`). Every other shortcut stays as
  Windows 3.1 had it rather than following the macOS conventions.

### Localization (English / Japanese)
- The display language comes from `[Settings] Language=` in `ReProgman.ini`.
  `auto` (the default) follows the OS UI culture (`ja*` means Japanese, anything
  else English), and `ja` / `en` set it explicitly. A change takes effect on the
  next start.
- The string catalog lives in the `Strings` class (App) as English/Japanese
  pairs. The language is resolved once at startup and then read from both XAML
  (`x:Static`) and code.
- The Japanese strings follow the Japanese Windows 3.1 conventions, writing
  access keys in parentheses as in "ファイル(F)". The default group name becomes
  "メイン".
- Japanese glyphs are drawn through the font fallbacks (`MS Gothic` /
  `Hiragino Kaku Gothic ProN`), which keeps the raster font look.
- On macOS the `.app`'s `Info.plist` declares `CFBundleLocalizations` (`en`,
  `ja`). Without it macOS resolves the bundle to its development region
  (English), so `auto` would come up English even in a Japanese environment.
- Note: because the default group name differs per language (`Main` / `メイン`),
  switching languages leaves that group's saved window and icon positions behind.

## Out of scope (not implemented)
- The contents of the help (everything in the Help menu except About is grayed
  out)
- Scroll bars for the MDI workspace itself
- The extended item properties (working directory, shortcut key, changing the
  icon)

## Screenshot mode for debugging
`ReProgman --screenshot <path> [--scene <name>]` draws the window into a PNG
about two seconds after starting and then exits. Scenes: `file-menu`,
`options-menu`, `window-menu`, `run`, `about`, `exit`, `small`, `free-icons`,
`new-object`, `item-props`, `move`, `maximized`. This mode saves no settings
(`Groups.ini` included) and draws popups inside the window (OverlayPopups) so
they can be captured. It works on both the Windows and the macOS backend.
Note: on a first run, with no `Groups.ini` yet, scanning the program folders and
loading the icons may not finish within those two seconds, and some icons are
captured as the substitute art.

## The application icon
Both icons of the shipped application are rendered from the group icon art the
app draws for itself (`GroupIconArt`), so they can never drift from the icon shown
inside the windows, and both are **committed as assets** rather than produced
during a build:

| Asset | Used by |
|---|---|
| `src/ReProgman/ReProgman.ico` | `<ApplicationIcon>` — embedded in the PE resources of the Windows executable, for a JIT build and a NativeAOT publish alike |
| `src/ReProgman/ReProgman.icns` | copied into `Contents/Resources/` of the macOS bundle by `publish-macos.sh` |

After changing the artwork, regenerate and commit both with:

```powershell
.\build\export-icons.ps1
```

The rendering lives in `tools/ReProgman.IconExport`, a small console tool that
references the app for the artwork and is never shipped; the script runs it with
`--ico <path> --icns <path>`. Keeping it out of `ReProgman` means the shipped
executable carries no export code, while the icons still come from the very same
`GroupIconArt` the windows draw.

They are committed rather than generated on the fly for two reasons:
`ApplicationIcon` has to exist before the build that would produce it, which is
circular, and rendering the art needs a window server, which a build machine (a
CI runner in particular) does not have.

- The `.ico` holds 32bpp DIBs at 16/32/48 px and a PNG at 256 px, assembled by
  `IcoWriter` / `IcoImage` in `ReProgman.Model`.
- The `.icns` holds the ten PNG slots `iconutil` would produce from a full
  `.iconset` (16 to 1024 px, with the `@2x` companions), assembled by
  `IcnsWriter` / `IcnsPngImage` in `ReProgman.Model`. It is the mirror of
  `IcnsDecoder`, and writing it ourselves is what lets the asset be regenerated
  on Windows as well, since `iconutil` only exists on macOS.
- Both writers are test-driven and free of any imaging library, so they work
  under AOT.
- The tool renders through Avalonia (`AppBuilder.Configure<Application>()` plus
  `SetupWithoutStarting()`), which is why it needs a desktop session to run.
- The window icon that shows up in the taskbar and elsewhere is built at runtime
  from the same art (`MainWindow.CreateWindowIcon`).

## NativeAOT builds
- `ReProgman` sets `PublishAot=true`, so publishing produces a NativeAOT native
  binary. A normal build, run or debugging session stays on the JIT.
- Publish on Windows with this script (the Visual Studio C++ workload is
  required):

  ```powershell
  .\build\publish-aot.ps1   # output: artifacts/publish-aot/
  ```

  A plain `dotnet publish` should work in principle, but the warnings that
  Visual Studio 2026 (v18)'s vcvarsall.bat writes to stderr are picked up by
  ILCompiler's linker discovery and corrupt the linker path, so the script
  imports the VC++ environment first and publishes with
  `IlcUseEnvironmentalTools=true`.

- Publish on macOS with this script (the Xcode Command Line Tools are required):

  ```bash
  ./build/publish-macos.sh   # output: artifacts/publish-macos/ReProgman.app
  ```

  It puts the NativeAOT binary and the native Skia / HarfBuzz / Avalonia
  libraries into `Contents/MacOS/` and generates `Info.plist` and the `.icns`.
  The Dock icon is the committed `ReProgman.icns` copied into the bundle, so the
  script needs neither a window server nor `iconutil`. Signing and notarization
  are out of
  scope; a downloaded `.app` needs
  `xattr -d com.apple.quarantine <path>`.
- `.github/workflows/build.yml` builds the same binaries on demand, leaves them
  as run artifacts and attaches them to a **draft** release: `ReProgman-win-x64.zip`,
  `ReProgman-win-arm64.zip` and `ReProgman-osx-arm64.zip`. It is
  `workflow_dispatch` only and takes an optional `tag` input, which defaults to
  `build-<run number>`. The release is created with `gh release create --draft`,
  so nothing is public and no tag exists until it is published by hand;
  dispatching again with the same tag refreshes the assets of the draft that is
  already there (`gh release upload --clobber`) instead of failing. Only that job
  is granted `contents: write`.
  The Windows jobs call `dotnet publish` directly rather than
  `build/publish-aot.ps1`, whose vcvarsall workaround is only needed with the
  local Visual Studio 2026 install, and win-arm64 cross-compiles from the x64
  runner because the runner image carries the ARM64 MSVC tools. The bundle is
  packed with `ditto`, because a zip of the bare `.app` directory would lose the
  executable bit.
- Implementation constraints that AOT imposes:
  - No reflection-based bindings: `AvaloniaUseCompiledBindingsByDefault=true`,
    and no `{Binding}` in ControlTheme setters (icon positions are set from code
    through `ContainerPrepared` + `PropertyChanged`).
  - No dependency on System.Drawing.Common. The HICON to bitmap conversion for
    shell icons goes through GDI P/Invoke (`GetIconInfo`/`GetDIBits`), and the
    alpha compositing for legacy icons (`IconPixels`) is tested in the model.
  - `ReProgman.Model` sets `IsAotCompatible=true` to enable the AOT analyzers.

## Platform-specific implementation
- `ReProgman.Model` is entirely OS-independent. The platform differences are
  absorbed in two places:
  - `IAppEntrySource` (`ShortcutFileSource` / `ApplicationBundleSource`): the
    rules for enumerating the program folders
  - `StorageLocation`: stepping out of the `.app` bundle, and the write probe
- In `ReProgman` the branches are limited to `IconLoader` (Windows:
  `IconExtractor` / macOS: `MacIconLoader`), `ShellLauncher`, `AppStorage`,
  `MainWindow.StartMenuRoots`, `MainWindow.UsesManualResize` and
  `Strings.MenuRefresh`.
- The P/Invokes are fetching shell icons on Windows, and reading the pointer
  position from CoreGraphics on macOS (`MacPointer`, used only by a sizing drag).
  The macOS icon and plist parsing is all managed code (in `ReProgman.Model`,
  test-driven).

## Dependencies and licenses
- Avalonia / Avalonia.Desktop / Avalonia.Themes.Simple / Avalonia.Diagnostics — MIT
- xunit.v3 / xunit.runner.visualstudio — Apache-2.0
