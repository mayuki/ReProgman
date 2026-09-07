namespace ReProgman.Model;

/// <summary>
/// Resolves the UI display language: an explicit "ja"/"en" setting wins,
/// anything else falls back to the OS UI culture.
/// </summary>
public static class DisplayLanguage
{
    public const string Japanese = "ja";
    public const string English = "en";
    public const string Auto = "auto";

    public static string Resolve(string? setting, string osCultureName) =>
        setting?.Trim().ToLowerInvariant() switch
        {
            "ja" or "japanese" => Japanese,
            "en" or "english" => English,
            _ => osCultureName.StartsWith("ja", StringComparison.OrdinalIgnoreCase) ? Japanese : English,
        };
}
