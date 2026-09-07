using ReProgman.Model;

namespace ReProgman.Model.Tests;

public class StartMenuMergeTests
{
    private static readonly ProgramItem Notepad = new("Notepad", @"C:\n.lnk");
    private static readonly ProgramItem Paint = new("Paint", @"C:\p.lnk");

    [Fact]
    public void Merge_IntoEmptyCatalog_AddsEverything()
    {
        var scanned = new[] { new ProgramGroup("Main", [Notepad, Paint]) };

        var result = StartMenuMerge.Merge([], scanned);

        Assert.Equal(1, result.AddedGroups);
        Assert.Equal(2, result.AddedItems);
        Assert.Equal("Main", Assert.Single(result.Groups).Name);
    }

    [Fact]
    public void Merge_WithIdenticalContent_AddsNothing()
    {
        var current = new[] { new ProgramGroup("Main", [Notepad]) };
        var scanned = new[] { new ProgramGroup("Main", [Notepad]) };

        var result = StartMenuMerge.Merge(current, scanned);

        Assert.Equal(0, result.AddedGroups);
        Assert.Equal(0, result.AddedItems);
        Assert.Equal("Notepad", Assert.Single(Assert.Single(result.Groups).Items).Name);
    }

    [Fact]
    public void Merge_AppendsMissingItemsToExistingGroup_MatchingCaseInsensitively()
    {
        var current = new[] { new ProgramGroup("Main", [Notepad]) };
        var scanned = new[] { new ProgramGroup("MAIN", [new ProgramItem("NOTEPAD", @"C:\other.lnk"), Paint]) };

        var result = StartMenuMerge.Merge(current, scanned);

        var group = Assert.Single(result.Groups);
        // The current group keeps its own casing and its own item paths.
        Assert.Equal("Main", group.Name);
        Assert.Equal(["Notepad", "Paint"], group.Items.Select(i => i.Name));
        Assert.Equal(@"C:\n.lnk", group.Items[0].Path);
        Assert.Equal(0, result.AddedGroups);
        Assert.Equal(1, result.AddedItems);
    }

    [Fact]
    public void Merge_AppendsNewGroupsAfterCurrentOnes()
    {
        var current = new[] { new ProgramGroup("Main", [Notepad]) };
        var scanned = new[] { new ProgramGroup("Games", [Paint]), new ProgramGroup("Main", []) };

        var result = StartMenuMerge.Merge(current, scanned);

        Assert.Equal(["Main", "Games"], result.Groups.Select(g => g.Name));
        Assert.Equal(1, result.AddedGroups);
        Assert.Equal(1, result.AddedItems);
    }

    [Fact]
    public void Merge_DoesNotTouchUserItemsMissingFromTheScan()
    {
        var userItem = new ProgramItem("My Tool", @"C:\tool.exe");
        var current = new[] { new ProgramGroup("Main", [userItem]) };
        var scanned = new[] { new ProgramGroup("Main", [Notepad]) };

        var result = StartMenuMerge.Merge(current, scanned);

        Assert.Equal(["My Tool", "Notepad"], Assert.Single(result.Groups).Items.Select(i => i.Name));
    }
}
