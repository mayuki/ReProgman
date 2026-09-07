using ReProgman.Model;

namespace ReProgman.Model.Tests;

public sealed class StartMenuScannerTests : IDisposable
{
    private readonly string _root1;
    private readonly string _root2;

    public StartMenuScannerTests()
    {
        _root1 = Path.Combine(Path.GetTempPath(), "ReProgmanTest_" + Guid.NewGuid().ToString("N"), "User");
        _root2 = Path.Combine(Path.GetTempPath(), "ReProgmanTest_" + Guid.NewGuid().ToString("N"), "Common");
        Directory.CreateDirectory(_root1);
        Directory.CreateDirectory(_root2);
    }

    public void Dispose()
    {
        foreach (var root in new[] { _root1, _root2 })
        {
            var parent = Path.GetDirectoryName(root)!;
            if (Directory.Exists(parent))
            {
                Directory.Delete(parent, recursive: true);
            }
        }
    }

    private static void Touch(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, []);
    }

    [Fact]
    public void Scan_TopLevelFoldersBecomeGroups()
    {
        Touch(Path.Combine(_root1, "Accessories", "Notepad.lnk"));
        Touch(Path.Combine(_root1, "Games", "Solitaire.lnk"));

        var groups = new StartMenuScanner(new ShortcutFileSource()).Scan([_root1]);

        Assert.Equal(["Accessories", "Games"], groups.Select(g => g.Name));
        Assert.Equal("Notepad", Assert.Single(groups[0].Items).Name);
    }

    [Fact]
    public void Scan_FilesAtRootGoToMainGroup()
    {
        Touch(Path.Combine(_root1, "File Manager.lnk"));
        Touch(Path.Combine(_root1, "Accessories", "Notepad.lnk"));

        var groups = new StartMenuScanner(new ShortcutFileSource()).Scan([_root1]);

        Assert.Equal(["Main", "Accessories"], groups.Select(g => g.Name));
        Assert.Equal("File Manager", Assert.Single(groups[0].Items).Name);
    }

    [Fact]
    public void Scan_MergesGroupsWithSameNameAcrossRoots()
    {
        Touch(Path.Combine(_root1, "Accessories", "Notepad.lnk"));
        Touch(Path.Combine(_root2, "accessories", "Paint.lnk"));

        var groups = new StartMenuScanner(new ShortcutFileSource()).Scan([_root1, _root2]);

        var group = Assert.Single(groups);
        Assert.Equal("Accessories", group.Name);
        Assert.Equal(["Notepad", "Paint"], group.Items.Select(i => i.Name));
    }

    [Fact]
    public void Scan_FirstRootWinsForDuplicateItemNames()
    {
        Touch(Path.Combine(_root1, "Accessories", "Notepad.lnk"));
        Touch(Path.Combine(_root2, "Accessories", "Notepad.lnk"));

        var groups = new StartMenuScanner(new ShortcutFileSource()).Scan([_root1, _root2]);

        var item = Assert.Single(Assert.Single(groups).Items);
        Assert.StartsWith(_root1, item.Path);
    }

    [Fact]
    public void Scan_FlattensNestedFoldersIntoTopLevelGroup()
    {
        Touch(Path.Combine(_root1, "Accessories", "System Tools", "Character Map.lnk"));

        var groups = new StartMenuScanner(new ShortcutFileSource()).Scan([_root1]);

        var group = Assert.Single(groups);
        Assert.Equal("Character Map", Assert.Single(group.Items).Name);
    }

    [Fact]
    public void Scan_SkipsEmptyGroupsAndNonShortcutFiles()
    {
        Directory.CreateDirectory(Path.Combine(_root1, "Empty"));
        Touch(Path.Combine(_root1, "OnlyJunk", "readme.txt"));
        Touch(Path.Combine(_root1, "OnlyJunk", "desktop.ini"));
        Touch(Path.Combine(_root1, "Real", "App.lnk"));

        var groups = new StartMenuScanner(new ShortcutFileSource()).Scan([_root1]);

        Assert.Equal("Real", Assert.Single(groups).Name);
    }

    [Fact]
    public void Scan_IncludesUrlShortcuts()
    {
        Touch(Path.Combine(_root1, "Links", "Example Site.url"));

        var groups = new StartMenuScanner(new ShortcutFileSource()).Scan([_root1]);

        Assert.Equal("Example Site", Assert.Single(Assert.Single(groups).Items).Name);
    }

    [Fact]
    public void Scan_SortsItemsAlphabetically()
    {
        Touch(Path.Combine(_root1, "Accessories", "Zebra.lnk"));
        Touch(Path.Combine(_root1, "Accessories", "Alpha.lnk"));

        var groups = new StartMenuScanner(new ShortcutFileSource()).Scan([_root1]);

        Assert.Equal(["Alpha", "Zebra"], Assert.Single(groups).Items.Select(i => i.Name));
    }

    [Fact]
    public void Scan_MissingRootIsIgnored()
    {
        var groups = new StartMenuScanner(new ShortcutFileSource()).Scan([Path.Combine(_root1, "DoesNotExist")]);

        Assert.Empty(groups);
    }

    [Fact]
    public void Scan_UsesLocalizedMainGroupNameForRootItemsAndSortsItFirst()
    {
        Touch(Path.Combine(_root1, "File Manager.lnk"));
        Touch(Path.Combine(_root1, "Accessories", "Notepad.lnk"));

        var groups = new StartMenuScanner(new ShortcutFileSource()).Scan([_root1], mainGroupName: "メイン");

        Assert.Equal(["メイン", "Accessories"], groups.Select(g => g.Name));
        Assert.Equal("File Manager", Assert.Single(groups[0].Items).Name);
    }
}
