namespace ReProgman.Model;

public sealed record MergeResult(IReadOnlyList<ProgramGroup> Groups, int AddedGroups, int AddedItems);

/// <summary>
/// Imports Start Menu additions into the user's group catalog: groups and items
/// missing from the catalog are appended, while everything the user already has
/// (including edited paths and names) is left untouched.
/// </summary>
public static class StartMenuMerge
{
    public static MergeResult Merge(IReadOnlyList<ProgramGroup> current, IReadOnlyList<ProgramGroup> scanned)
    {
        var addedGroups = 0;
        var addedItems = 0;
        var result = new List<ProgramGroup>();

        foreach (var group in current)
        {
            var scannedGroup = scanned.FirstOrDefault(
                g => string.Equals(g.Name, group.Name, StringComparison.OrdinalIgnoreCase));
            if (scannedGroup is null)
            {
                result.Add(group);
                continue;
            }

            var knownNames = group.Items.Select(i => i.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var newItems = scannedGroup.Items.Where(i => !knownNames.Contains(i.Name)).ToList();
            addedItems += newItems.Count;
            result.Add(newItems.Count == 0 ? group : group with { Items = [.. group.Items, .. newItems] });
        }

        foreach (var scannedGroup in scanned)
        {
            if (!current.Any(g => string.Equals(g.Name, scannedGroup.Name, StringComparison.OrdinalIgnoreCase)))
            {
                result.Add(scannedGroup);
                addedGroups++;
                addedItems += scannedGroup.Items.Count;
            }
        }

        return new MergeResult(result, addedGroups, addedItems);
    }
}
