using ReProgman.Model;

namespace ReProgman.Model.Tests;

public class IniDocumentTests
{
    [Fact]
    public void Parse_ReadsValuesFromSections()
    {
        var ini = IniDocument.Parse(
            """
            [Settings]
            Window=68,63,636,421,normal

            [Groups]
            Group1=Main
            """);

        Assert.Equal("68,63,636,421,normal", ini.Get("Settings", "Window"));
        Assert.Equal("Main", ini.Get("Groups", "Group1"));
    }

    [Fact]
    public void Get_ReturnsNullForMissingSectionOrKey()
    {
        var ini = IniDocument.Parse("[Settings]\nWindow=1,2,3,4,normal\n");

        Assert.Null(ini.Get("Nope", "Window"));
        Assert.Null(ini.Get("Settings", "Nope"));
    }

    [Fact]
    public void Get_IsCaseInsensitiveForSectionAndKey()
    {
        var ini = IniDocument.Parse("[Settings]\nWindow=abc\n");

        Assert.Equal("abc", ini.Get("settings", "WINDOW"));
    }

    [Fact]
    public void Parse_IgnoresCommentsAndBlankLines()
    {
        var ini = IniDocument.Parse(
            """
            ; comment line
            [Settings]
            ; another comment
            Key=Value
            """);

        Assert.Equal("Value", ini.Get("Settings", "Key"));
    }

    [Fact]
    public void Parse_KeepsEqualsSignInValue()
    {
        var ini = IniDocument.Parse("[S]\nKey=a=b=c\n");

        Assert.Equal("a=b=c", ini.Get("S", "Key"));
    }

    [Fact]
    public void Set_AddsSectionAndKey()
    {
        var ini = new IniDocument();
        ini.Set("Settings", "Window", "1,2,3,4,normal");

        Assert.Equal("1,2,3,4,normal", ini.Get("Settings", "Window"));
    }

    [Fact]
    public void Set_OverwritesExistingValue()
    {
        var ini = IniDocument.Parse("[S]\nKey=old\n");
        ini.Set("S", "Key", "new");

        Assert.Equal("new", ini.Get("S", "Key"));
    }

    [Fact]
    public void ToText_RoundTripsThroughParse()
    {
        var ini = new IniDocument();
        ini.Set("Settings", "Window", "1,2,3,4,maximized");
        ini.Set("Group.Main", "Window", "5,6,7,8,normal");

        var reparsed = IniDocument.Parse(ini.ToText());

        Assert.Equal("1,2,3,4,maximized", reparsed.Get("Settings", "Window"));
        Assert.Equal("5,6,7,8,normal", reparsed.Get("Group.Main", "Window"));
    }

    [Fact]
    public void Sections_ReturnsSectionNamesInDocumentOrder()
    {
        var ini = IniDocument.Parse("[B]\nk=1\n[A]\nk=2\n");

        Assert.Equal(new[] { "B", "A" }, ini.Sections);
    }

    [Fact]
    public void Keys_ReturnsKeysOfSectionInDocumentOrder()
    {
        var ini = IniDocument.Parse("[S]\nSecond=2\nFirst=1\n");

        Assert.Equal(new[] { "Second", "First" }, ini.Keys("S"));
    }
}
