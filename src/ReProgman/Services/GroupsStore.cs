using ReProgman.Model;

namespace ReProgman.Services;

/// <summary>
/// Persists the group catalog as Groups.ini in the storage folder. After the
/// initial Start Menu scan this file is the source of truth for groups and
/// items; the Start Menu itself is never written to.
/// </summary>
public static class GroupsStore
{
    public static string GroupsPath => Path.Combine(AppStorage.Directory, "Groups.ini");

    public static bool Exists => File.Exists(GroupsPath);

    public static IReadOnlyList<ProgramGroup> Load()
    {
        try
        {
            return Exists ? GroupCatalog.Load(File.ReadAllText(GroupsPath)) : [];
        }
        catch (IOException)
        {
            return [];
        }
    }

    public static void Save(IEnumerable<ProgramGroup> groups)
    {
        try
        {
            File.WriteAllText(GroupsPath, GroupCatalog.Save(groups));
        }
        catch (IOException)
        {
            // A read-only install location silently loses edits, matching the
            // behavior of the settings store.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
