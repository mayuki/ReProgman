using ReProgman.Model;

namespace ReProgman.Model.Tests;

public class ReProgmanSettingsTests
{
    [Fact]
    public void Defaults_AreProgmanLike()
    {
        var settings = new ReProgmanSettings();

        Assert.True(settings.SaveSettingsOnExit);
        Assert.True(settings.AutoArrange);
        Assert.False(settings.MinimizeOnUse);
        Assert.Null(settings.MainWindow);
        Assert.Empty(settings.GroupOrder);
    }

    [Fact]
    public void SaveAndLoad_RoundTripsAllValues()
    {
        var settings = new ReProgmanSettings
        {
            SaveSettingsOnExit = false,
            AutoArrange = false,
            MinimizeOnUse = true,
            MainWindow = new WindowPlacement(68, 63, 636, 421, WindowStateKind.Normal),
            ActiveGroup = "Main",
        };
        settings.GroupOrder.Add("Accessories");
        settings.GroupOrder.Add("Main");
        settings.GroupWindows["Main"] = new WindowPlacement(10, 10, 400, 200, WindowStateKind.Normal);
        settings.GroupWindows["Accessories"] = new WindowPlacement(0, 0, 0, 0, WindowStateKind.Minimized);

        var loaded = ReProgmanSettings.Load(settings.Save());

        Assert.False(loaded.SaveSettingsOnExit);
        Assert.False(loaded.AutoArrange);
        Assert.True(loaded.MinimizeOnUse);
        Assert.Equal(settings.MainWindow, loaded.MainWindow);
        Assert.Equal("Main", loaded.ActiveGroup);
        Assert.Equal(new[] { "Accessories", "Main" }, loaded.GroupOrder);
        Assert.Equal(settings.GroupWindows["Main"], loaded.GroupWindows["Main"]);
        Assert.Equal(settings.GroupWindows["Accessories"], loaded.GroupWindows["Accessories"]);
    }

    [Fact]
    public void Load_EmptyText_ReturnsDefaults()
    {
        var loaded = ReProgmanSettings.Load("");

        Assert.True(loaded.SaveSettingsOnExit);
        Assert.Null(loaded.MainWindow);
        Assert.Empty(loaded.GroupWindows);
    }

    [Fact]
    public void Load_IgnoresBrokenPlacementValues()
    {
        var loaded = ReProgmanSettings.Load(
            """
            [Settings]
            Window=broken
            [Group.Main]
            Window=also broken
            """);

        Assert.Null(loaded.MainWindow);
        Assert.Empty(loaded.GroupWindows);
    }

    [Fact]
    public void Language_DefaultsToNullAndSavesAsAuto()
    {
        var settings = new ReProgmanSettings();

        Assert.Null(settings.Language);
        // "auto" is written out so users can discover the setting in the INI.
        Assert.Contains("Language=auto", settings.Save());
    }

    [Theory]
    [InlineData("auto", null)]
    [InlineData("", null)]
    [InlineData("ja", "ja")]
    [InlineData("en", "en")]
    public void Language_RoundTrips(string stored, string? expected)
    {
        var loaded = ReProgmanSettings.Load($"[Settings]\nLanguage={stored}\n");

        Assert.Equal(expected, loaded.Language);
    }

    [Fact]
    public void Language_ExplicitValueIsSaved()
    {
        var settings = new ReProgmanSettings { Language = "ja" };

        var loaded = ReProgmanSettings.Load(settings.Save());

        Assert.Equal("ja", loaded.Language);
    }

    [Fact]
    public void SaveAndLoad_RoundTripsIconPositions()
    {
        var settings = new ReProgmanSettings();
        settings.IconPositions["Main"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Notepad"] = new IconPosition(74, 0),
            ["Foo, Bar"] = new IconPosition(0, 62),
        };
        settings.IconPositions["Games"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Solitaire"] = new IconPosition(148, 124),
        };

        var loaded = ReProgmanSettings.Load(settings.Save());

        Assert.Equal(new IconPosition(74, 0), loaded.IconPositions["Main"]["Notepad"]);
        Assert.Equal(new IconPosition(0, 62), loaded.IconPositions["Main"]["Foo, Bar"]);
        Assert.Equal(new IconPosition(148, 124), loaded.IconPositions["Games"]["Solitaire"]);
    }

    [Fact]
    public void Load_IgnoresBrokenIconEntries()
    {
        var loaded = ReProgmanSettings.Load(
            """
            [Icons.Main]
            Icon1=broken
            Icon2=1,2
            Icon3=x,y,Name
            Icon4=5,6,Notepad
            """);

        var main = Assert.Single(loaded.IconPositions).Value;
        Assert.Equal(new IconPosition(5, 6), Assert.Single(main).Value);
        Assert.True(main.ContainsKey("Notepad"));
    }

    [Fact]
    public void GroupNames_WithDotsAndBrackets_RoundTrip()
    {
        var settings = new ReProgmanSettings();
        settings.GroupWindows["My.Group [x]"] = new WindowPlacement(1, 2, 3, 4, WindowStateKind.Normal);
        settings.GroupOrder.Add("My.Group [x]");

        var loaded = ReProgmanSettings.Load(settings.Save());

        Assert.Equal(new WindowPlacement(1, 2, 3, 4, WindowStateKind.Normal), loaded.GroupWindows["My.Group [x]"]);
        Assert.Equal(new[] { "My.Group [x]" }, loaded.GroupOrder);
    }
}
