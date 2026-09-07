namespace ReProgman.Model;

/// <summary>
/// Serializes the program groups to Groups.ini next to the executable. After the
/// first Start Menu scan this file is the source of truth: user edits are stored
/// here and are never written back to the Start Menu.
/// </summary>
public static class GroupCatalog
{
    private const string GroupsSection = "Groups";
    private const string GroupSectionPrefix = "Group.";

    // '|' separates the display name from the item path. Windows paths cannot
    // contain '|' but macOS paths can, so a pipe inside the path is doubled and
    // the value is split at the first pipe whose remainder is a well-formed
    // (evenly doubled) path. On values without doubling that is the same split
    // the Windows-only format used, which keeps old Groups.ini files readable.
    private const char Separator = '|';

    public static string Save(IEnumerable<ProgramGroup> groups)
    {
        var ini = new IniDocument();
        var groupIndex = 0;
        foreach (var group in groups)
        {
            ini.Set(GroupsSection, $"Group{++groupIndex}", group.Name);
            for (var i = 0; i < group.Items.Count; i++)
            {
                ini.Set(GroupSectionPrefix + group.Name, $"Item{i + 1}", FormatItem(group.Items[i]));
            }
        }

        return ini.ToText();
    }

    public static IReadOnlyList<ProgramGroup> Load(string text)
    {
        var ini = IniDocument.Parse(text);
        var groups = new List<ProgramGroup>();
        foreach (var key in ini.Keys(GroupsSection))
        {
            if (ini.Get(GroupsSection, key) is not { Length: > 0 } name)
            {
                continue;
            }

            var section = GroupSectionPrefix + name;
            var items = new List<ProgramItem>();
            foreach (var itemKey in ini.Keys(section))
            {
                if (ini.Get(section, itemKey) is { } value && ParseItem(value) is { } item)
                {
                    items.Add(item);
                }
            }

            groups.Add(new ProgramGroup(name, items));
        }

        return groups;
    }

    private static string FormatItem(ProgramItem item) =>
        item.Name + Separator + item.Path.Replace("|", "||");

    private static ProgramItem? ParseItem(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] != Separator || !IsEscapedPath(value, i + 1))
            {
                continue;
            }

            var name = value[..i];
            var path = value[(i + 1)..].Replace("||", "|");
            return name.Length > 0 && path.Length > 0 ? new ProgramItem(name, path) : null;
        }

        return null;
    }

    /// <summary>
    /// True when every run of pipes from <paramref name="start"/> onwards has an
    /// even length, i.e. the remainder can only be an escaped path.
    /// </summary>
    private static bool IsEscapedPath(string value, int start)
    {
        for (var i = start; i < value.Length; i++)
        {
            if (value[i] != Separator)
            {
                continue;
            }

            var run = 0;
            while (i < value.Length && value[i] == Separator)
            {
                run++;
                i++;
            }

            if (run % 2 != 0)
            {
                return false;
            }

            i--;
        }

        return true;
    }
}
