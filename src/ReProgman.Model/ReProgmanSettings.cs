namespace ReProgman.Model;

/// <summary>
/// Portable application settings, stored as an INI file next to the executable
/// instead of the registry so that the application stays self-contained.
/// </summary>
public sealed class ReProgmanSettings
{
    private const string SettingsSection = "Settings";
    private const string GroupsSection = "Groups";
    private const string GroupSectionPrefix = "Group.";
    private const string IconsSectionPrefix = "Icons.";

    public bool SaveSettingsOnExit { get; set; } = true;
    public bool AutoArrange { get; set; } = true;
    public bool MinimizeOnUse { get; set; }
    public WindowPlacement? MainWindow { get; set; }
    public string? ActiveGroup { get; set; }

    /// <summary>Display language ("ja"/"en"); null means "auto" (follow the OS UI culture).</summary>
    public string? Language { get; set; }

    /// <summary>Group names ordered from bottom to top of the MDI z-order.</summary>
    public List<string> GroupOrder { get; } = [];

    public Dictionary<string, WindowPlacement> GroupWindows { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Per-group icon positions, keyed by group name then item name.</summary>
    public Dictionary<string, Dictionary<string, IconPosition>> IconPositions { get; } = new(StringComparer.OrdinalIgnoreCase);

    public string Save()
    {
        var ini = new IniDocument();
        if (MainWindow is { } main)
        {
            ini.Set(SettingsSection, "Window", main.ToIniString());
        }

        ini.Set(SettingsSection, "SaveSettings", SaveSettingsOnExit ? "1" : "0");
        ini.Set(SettingsSection, "AutoArrange", AutoArrange ? "1" : "0");
        ini.Set(SettingsSection, "MinimizeOnUse", MinimizeOnUse ? "1" : "0");
        // Always written so that the knob is discoverable in the INI file.
        ini.Set(SettingsSection, "Language", Language ?? DisplayLanguage.Auto);
        if (ActiveGroup is not null)
        {
            ini.Set(SettingsSection, "ActiveGroup", ActiveGroup);
        }

        for (var i = 0; i < GroupOrder.Count; i++)
        {
            ini.Set(GroupsSection, $"Group{i + 1}", GroupOrder[i]);
        }

        foreach (var (name, placement) in GroupWindows)
        {
            ini.Set(GroupSectionPrefix + name, "Window", placement.ToIniString());
        }

        foreach (var (groupName, icons) in IconPositions)
        {
            var index = 0;
            foreach (var (itemName, position) in icons)
            {
                // The item name goes last so that names containing commas survive.
                ini.Set(IconsSectionPrefix + groupName, $"Icon{++index}", $"{position.X},{position.Y},{itemName}");
            }
        }

        return ini.ToText();
    }

    public static ReProgmanSettings Load(string text)
    {
        var ini = IniDocument.Parse(text);
        var settings = new ReProgmanSettings
        {
            SaveSettingsOnExit = ReadFlag(ini, "SaveSettings", defaultValue: true),
            AutoArrange = ReadFlag(ini, "AutoArrange", defaultValue: true),
            MinimizeOnUse = ReadFlag(ini, "MinimizeOnUse", defaultValue: false),
            ActiveGroup = ini.Get(SettingsSection, "ActiveGroup"),
        };

        var language = ini.Get(SettingsSection, "Language")?.Trim();
        if (!string.IsNullOrEmpty(language) &&
            !string.Equals(language, DisplayLanguage.Auto, StringComparison.OrdinalIgnoreCase))
        {
            settings.Language = language;
        }

        if (WindowPlacement.TryParse(ini.Get(SettingsSection, "Window"), out var main))
        {
            settings.MainWindow = main;
        }

        foreach (var key in ini.Keys(GroupsSection))
        {
            if (ini.Get(GroupsSection, key) is { Length: > 0 } name)
            {
                settings.GroupOrder.Add(name);
            }
        }

        foreach (var section in ini.Sections)
        {
            if (section.StartsWith(GroupSectionPrefix, StringComparison.OrdinalIgnoreCase) &&
                WindowPlacement.TryParse(ini.Get(section, "Window"), out var placement))
            {
                settings.GroupWindows[section[GroupSectionPrefix.Length..]] = placement;
            }

            if (section.StartsWith(IconsSectionPrefix, StringComparison.OrdinalIgnoreCase))
            {
                LoadIconSection(ini, section, settings);
            }
        }

        return settings;
    }

    private static void LoadIconSection(IniDocument ini, string section, ReProgmanSettings settings)
    {
        var groupName = section[IconsSectionPrefix.Length..];
        foreach (var key in ini.Keys(section))
        {
            var parts = ini.Get(section, key)?.Split(',', 3);
            if (parts is { Length: 3 } &&
                int.TryParse(parts[0], System.Globalization.CultureInfo.InvariantCulture, out var x) &&
                int.TryParse(parts[1], System.Globalization.CultureInfo.InvariantCulture, out var y) &&
                parts[2].Length > 0)
            {
                if (!settings.IconPositions.TryGetValue(groupName, out var icons))
                {
                    icons = new Dictionary<string, IconPosition>(StringComparer.OrdinalIgnoreCase);
                    settings.IconPositions[groupName] = icons;
                }

                icons[parts[2]] = new IconPosition(x, y);
            }
        }
    }

    private static bool ReadFlag(IniDocument ini, string key, bool defaultValue) =>
        ini.Get(SettingsSection, key) switch
        {
            "1" => true,
            "0" => false,
            _ => defaultValue,
        };
}
