using ReProgman.Model;

namespace ReProgman.Model.Tests;

public class DisplayLanguageTests
{
    [Theory]
    [InlineData(null, "ja-JP", "ja")]
    [InlineData("", "ja-JP", "ja")]
    [InlineData("auto", "ja-JP", "ja")]
    [InlineData(null, "en-US", "en")]
    [InlineData("auto", "en-US", "en")]
    [InlineData(null, "fr-FR", "en")]
    public void Resolve_AutoFollowsOsCulture(string? setting, string osCulture, string expected)
    {
        Assert.Equal(expected, DisplayLanguage.Resolve(setting, osCulture));
    }

    [Theory]
    [InlineData("ja", "en-US", "ja")]
    [InlineData("JA", "en-US", "ja")]
    [InlineData(" ja ", "en-US", "ja")]
    [InlineData("japanese", "en-US", "ja")]
    [InlineData("en", "ja-JP", "en")]
    [InlineData("English", "ja-JP", "en")]
    public void Resolve_ExplicitSettingWinsOverOs(string setting, string osCulture, string expected)
    {
        Assert.Equal(expected, DisplayLanguage.Resolve(setting, osCulture));
    }

    [Theory]
    [InlineData("de", "ja-JP", "ja")]
    [InlineData("nonsense", "en-US", "en")]
    public void Resolve_UnknownSettingFallsBackToAuto(string setting, string osCulture, string expected)
    {
        Assert.Equal(expected, DisplayLanguage.Resolve(setting, osCulture));
    }
}
