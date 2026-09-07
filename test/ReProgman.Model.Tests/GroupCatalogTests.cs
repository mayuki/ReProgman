using ReProgman.Model;

namespace ReProgman.Model.Tests;

public class GroupCatalogTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsGroupsItemsAndOrder()
    {
        var groups = new[]
        {
            new ProgramGroup("Zebra", [new ProgramItem("Notepad", @"C:\n.lnk"), new ProgramItem("Paint", @"C:\p.lnk")]),
            new ProgramGroup("Alpha", [new ProgramItem("Solitaire", @"C:\s.lnk")]),
        };

        var loaded = GroupCatalog.Load(GroupCatalog.Save(groups));

        Assert.Equal(["Zebra", "Alpha"], loaded.Select(g => g.Name));
        Assert.Equal(["Notepad", "Paint"], loaded[0].Items.Select(i => i.Name));
        Assert.Equal(@"C:\n.lnk", loaded[0].Items[0].Path);
        Assert.Equal("Solitaire", Assert.Single(loaded[1].Items).Name);
    }

    [Fact]
    public void SaveAndLoad_KeepsEmptyGroups()
    {
        var loaded = GroupCatalog.Load(GroupCatalog.Save([new ProgramGroup("Empty", [])]));

        var group = Assert.Single(loaded);
        Assert.Equal("Empty", group.Name);
        Assert.Empty(group.Items);
    }

    [Fact]
    public void SaveAndLoad_ItemNameMayContainThePipeSeparator()
    {
        // Paths cannot contain '|' on Windows, but user-edited descriptions can.
        var groups = new[] { new ProgramGroup("G", [new ProgramItem("A|B", @"C:\x.lnk")]) };

        var loaded = GroupCatalog.Load(GroupCatalog.Save(groups));

        var item = Assert.Single(Assert.Single(loaded).Items);
        Assert.Equal("A|B", item.Name);
        Assert.Equal(@"C:\x.lnk", item.Path);
    }

    [Fact]
    public void Load_SkipsBrokenItemEntries()
    {
        var loaded = GroupCatalog.Load(
            """
            [Groups]
            Group1=G
            [Group.G]
            Item1=nopipe
            Item2=|C:\pathonly.lnk
            Item3=nameonly|
            Item4=Ok|C:\ok.lnk
            """);

        Assert.Equal("Ok", Assert.Single(Assert.Single(loaded).Items).Name);
    }

    [Fact]
    public void Load_EmptyText_ReturnsNoGroups()
    {
        Assert.Empty(GroupCatalog.Load(""));
    }

    [Fact]
    public void SaveAndLoad_ItemPathMayContainThePipeSeparator()
    {
        // macOS file names may contain '|', so the path half is escaped.
        var groups = new[] { new ProgramGroup("G", [new ProgramItem("Weird", "/Applications/A|B.app")]) };

        var loaded = GroupCatalog.Load(GroupCatalog.Save(groups));

        var item = Assert.Single(Assert.Single(loaded).Items);
        Assert.Equal("Weird", item.Name);
        Assert.Equal("/Applications/A|B.app", item.Path);
    }

    [Fact]
    public void SaveAndLoad_BothHalvesMayContainThePipeSeparator()
    {
        var groups = new[] { new ProgramGroup("G", [new ProgramItem("A|B", "/Applications/C|D.app")]) };

        var loaded = GroupCatalog.Load(GroupCatalog.Save(groups));

        var item = Assert.Single(Assert.Single(loaded).Items);
        Assert.Equal("A|B", item.Name);
        Assert.Equal("/Applications/C|D.app", item.Path);
    }

    [Fact]
    public void Save_LeavesPipeFreePathsUnescaped()
    {
        // Existing Groups.ini files must keep round-tripping byte for byte.
        var text = GroupCatalog.Save([new ProgramGroup("G", [new ProgramItem("Notepad", @"C:\n.lnk")])]);

        Assert.Contains(@"Item1=Notepad|C:\n.lnk", text);
    }

    [Fact]
    public void Load_ReadsLegacyValuesWrittenBeforeEscaping()
    {
        var loaded = GroupCatalog.Load(
            """
            [Groups]
            Group1=G
            [Group.G]
            Item1=A|B|C:\x.lnk
            """);

        var item = Assert.Single(Assert.Single(loaded).Items);
        Assert.Equal("A|B", item.Name);
        Assert.Equal(@"C:\x.lnk", item.Path);
    }
}
