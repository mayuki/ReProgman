namespace ReProgman;

/// <summary>
/// Static UI string catalog, selected once at startup before any window loads.
/// Japanese strings follow the Windows 3.1J convention of showing the access
/// key in parentheses, e.g. "ファイル(F)".
/// </summary>
public static class Strings
{
    private static bool _japanese;

    public static void UseJapanese(bool japanese) => _japanese = japanese;

    private static string T(string english, string japanese) => _japanese ? japanese : english;

    // Window
    public static string AppTitle => T("Program Manager", "プログラム マネージャ");
    public static string MainGroupName => T("Main", "メイン");

    // File menu
    public static string MenuFile => T("_File", "ファイル(_F)");
    public static string MenuNew => T("_New...", "新規(_N)...");
    public static string MenuOpen => T("_Open", "開く(_O)");
    public static string MenuMove => T("_Move...", "移動(_M)...");
    public static string MenuCopy => T("_Copy...", "コピー(_C)...");
    public static string MenuDelete => T("_Delete", "削除(_D)");
    public static string MenuProperties => T("P_roperties...", "プロパティ(_P)...");
    public static string MenuRun => T("R_un...", "ファイル名を指定して実行(_R)...");
    public static string MenuExit => T("E_xit ReProgman...", "ReProgman の終了(_X)...");

    // File menu: editing
    // The command scans whatever the platform calls its program folder, so the
    // label names the place the user actually knows.
    public static string MenuRefresh => OperatingSystem.IsMacOS()
        ? T("Re_fresh from Applications", "アプリケーションから取り込み(_F)")
        : T("Re_fresh from Start Menu", "スタートメニューから取り込み(_F)");
    public static string NewTitle => T("New Program Object", "新規登録");
    public static string NewChoiceLabel => T("New", "新規");
    public static string NewProgramGroup => T("Program _Group", "プログラム グループ(_G)");
    public static string NewProgramItem => T("Program _Item", "プログラム アイテム(_I)");
    public static string GroupPropsTitle => T("Program Group Properties", "プログラム グループのプロパティ");
    public static string ItemPropsTitle => T("Program Item Properties", "プログラム アイテムのプロパティ");
    public static string LabelDescription => T("_Description:", "説明(_D):");
    public static string MoveTitle => T("Move Program Item", "プログラム アイテムの移動");
    public static string CopyTitle => T("Copy Program Item", "プログラム アイテムのコピー");
    public static string DeleteTitle => T("Delete", "削除");

    public static string ItemCaption(string name) => string.Format(T("Program Item:  {0}", "プログラム アイテム:  {0}"), name);
    public static string FromGroupCaption(string name) => string.Format(T("From Program Group:  {0}", "元のグループ:  {0}"), name);
    public static string MoveToLabel => T("_To Group:", "移動先のグループ(_T):");
    public static string CopyToLabel => T("_To Group:", "コピー先のグループ(_T):");

    public static string ConfirmDeleteItem(string name) => string.Format(
        T("Are you sure you want to delete the item '{0}'?", "アイテム '{0}' を削除してもよろしいですか?"), name);

    public static string ConfirmDeleteGroup(string name) => string.Format(
        T("Are you sure you want to delete the group '{0}'?", "グループ '{0}' を削除してもよろしいですか?"), name);

    public static string ErrGroupExists(string name) => string.Format(
        T("The group '{0}' already exists.", "グループ '{0}' は既に存在します。"), name);

    public static string ErrItemExists(string name, string group) => string.Format(
        T("The item '{0}' already exists in the group '{1}'.", "アイテム '{0}' はグループ '{1}' に既に存在します。"), name, group);

    public static string ErrEmptyDescription => T("The description cannot be empty.", "説明を入力してください。");

    public static string RefreshResult(int groups, int items) => string.Format(
        OperatingSystem.IsMacOS()
            ? T("Added {0} group(s) and {1} item(s) from Applications.", "アプリケーションから {0} 個のグループと {1} 個のアイテムを取り込みました。")
            : T("Added {0} group(s) and {1} item(s) from the Start Menu.", "スタート メニューから {0} 個のグループと {1} 個のアイテムを取り込みました。"),
        groups,
        items);

    public static string RefreshNothing => T("No new groups or items were found.", "新しい項目はありませんでした。");

    // Options menu
    public static string MenuOptions => T("_Options", "オプション(_O)");
    public static string MenuAutoArrange => T("_Auto Arrange", "アイコンの自動整列(_A)");
    public static string MenuMinimizeOnUse => T("_Minimize on Use", "実行時にアイコン化(_M)");
    public static string MenuSaveSettings => T("_Save Settings on Exit", "終了時に設定を保存(_S)");

    // Window menu
    public static string MenuWindow => T("_Window", "ウィンドウ(_W)");
    public static string MenuCascade => T("_Cascade", "重ねて表示(_C)");
    public static string MenuTile => T("_Tile", "並べて表示(_T)");
    public static string MenuArrangeIcons => T("_Arrange Icons", "アイコンの整列(_A)");

    // Help menu
    public static string MenuHelp => T("_Help", "ヘルプ(_H)");
    public static string MenuHelpContents => T("_Contents", "目次(_C)");
    public static string MenuHelpSearch => T("_Search for Help on...", "トピックの検索(_S)...");
    public static string MenuHelpHowTo => T("_How to Use Help", "ヘルプの使い方(_H)");
    public static string MenuHelpTutorial => T("_Windows Tutorial", "Windows チュートリアル(_W)");
    public static string MenuHelpAbout => T("_About Program Manager...", "プログラム マネージャについて(_A)...");

    // System menus
    public static string SysRestore => T("_Restore", "元のサイズに戻す(_R)");
    public static string SysMove => T("_Move", "移動(_M)");
    public static string SysSize => T("_Size", "サイズ変更(_S)");
    public static string SysMinimize => T("Mi_nimize", "アイコン化(_N)");
    public static string SysMaximize => T("Ma_ximize", "最大化(_X)");
    public static string SysClose => T("_Close", "閉じる(_C)");
    public static string SysSwitchTo => T("Switch _To...", "タスクの切り替え(_T)...");

    // Buttons
    public static string ButtonOk => "OK";
    public static string ButtonCancel => T("Cancel", "キャンセル");
    public static string ButtonYes => T("_Yes", "はい(_Y)");
    public static string ButtonNo => T("_No", "いいえ(_N)");
    public static string ButtonBrowse => T("_Browse...", "参照(_B)...");
    public static string ButtonHelp => T("_Help", "ヘルプ(_H)");

    // Run dialog
    public static string RunTitle => T("Run", "ファイル名を指定して実行");
    public static string RunCommandLine => T("_Command Line:", "コマンド ライン(_C):");
    public static string RunMinimized => T("Run _Minimized", "アイコンの状態で実行(_M)");
    public static string BrowseTitle => T("Browse", "参照");
    public static string FilterPrograms => T("Programs", "プログラム");
    public static string FilterAllFiles => T("All Files", "すべてのファイル");

    // About dialog
    public static string AboutTitle => T("About Program Manager", "プログラム マネージャについて");
    public static string AboutVersion => T("Version 1.0", "バージョン 1.0");
    public static string AboutLine1 => T("A Windows 3.1 Program Manager look-alike", "Windows 3.1 のプログラム マネージャを再現した");
    public static string AboutLine2 => T("built with .NET and Avalonia.", ".NET と Avalonia 製のアプリケーションです。");

    // Exit dialog
    public static string ExitTitle => T("Exit ReProgman", "ReProgman の終了");
    public static string ExitMessage => T("This will end your ReProgman session.", "これで ReProgman セッションを終了します。");

    public static string LaunchError(string path) => string.Format(
        T(
            "Cannot find file '{0}' (or one of its components). Check to ensure the path and filename are correct.",
            "ファイル '{0}' (またはその構成ファイル) が見つかりません。パスとファイル名が正しいことを確認してください。"),
        path);
}
