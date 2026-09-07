namespace ReProgman.Model;

/// <summary>
/// Builds Program Manager groups from the platform's program folders (Start Menu
/// "Programs" on Windows, /Applications on macOS). Each top-level folder becomes
/// a group; loose entries at the root go to "Main", mirroring the default group
/// of the original Program Manager.
/// </summary>
public sealed class StartMenuScanner(IAppEntrySource source)
{
    public const string MainGroupName = "Main";

    public IReadOnlyList<ProgramGroup> Scan(IEnumerable<string> programsRoots, string? mainGroupName = null)
    {
        var mainName = mainGroupName ?? MainGroupName;

        // Group name -> (first-seen display name, item name -> item). The first root wins for
        // duplicate item names so that per-user entries take priority over machine-wide ones.
        var groups = new Dictionary<string, (string DisplayName, Dictionary<string, ProgramItem> Items)>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in programsRoots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            var rootEntries = source.Enumerate(root, recursive: false).ToList();
            foreach (var entry in rootEntries)
            {
                AddItem(groups, mainName, entry);
            }

            // On macOS an entry is a directory itself (a .app bundle); those already
            // went into the default group and must not also become groups.
            var entryDirectories = rootEntries.Select(e => e.Path).ToHashSet(StringComparer.Ordinal);

            foreach (var directory in new DirectoryInfo(root).EnumerateDirectories())
            {
                if (IsHidden(directory) || entryDirectories.Contains(directory.FullName))
                {
                    continue;
                }

                foreach (var entry in source.Enumerate(directory.FullName, recursive: true))
                {
                    AddItem(groups, directory.Name, entry);
                }
            }
        }

        return groups.Values
            .Where(g => g.Items.Count > 0)
            .OrderBy(g => !string.Equals(g.DisplayName, mainName, StringComparison.OrdinalIgnoreCase))
            .ThenBy(g => g.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(g => new ProgramGroup(
                g.DisplayName,
                g.Items.Values.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToArray()))
            .ToArray();
    }

    private static void AddItem(
        Dictionary<string, (string DisplayName, Dictionary<string, ProgramItem> Items)> groups,
        string groupName,
        AppEntry entry)
    {
        if (!groups.TryGetValue(groupName, out var group))
        {
            group = (groupName, new Dictionary<string, ProgramItem>(StringComparer.OrdinalIgnoreCase));
            groups[groupName] = group;
        }

        if (!group.Items.ContainsKey(entry.Name))
        {
            group.Items[entry.Name] = new ProgramItem(entry.Name, entry.Path);
        }
    }

    private static bool IsHidden(FileSystemInfo info) =>
        (info.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0;
}
