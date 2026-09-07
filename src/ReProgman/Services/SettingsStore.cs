using ReProgman.Model;

namespace ReProgman.Services;

/// <summary>
/// Loads and saves <see cref="ReProgmanSettings"/> as ReProgman.ini in the storage folder,
/// keeping the application portable (no registry or per-user profile writes).
/// </summary>
public static class SettingsStore
{
    public static string SettingsPath => Path.Combine(AppStorage.Directory, "ReProgman.ini");

    public static ReProgmanSettings Load()
    {
        try
        {
            return File.Exists(SettingsPath)
                ? ReProgmanSettings.Load(File.ReadAllText(SettingsPath))
                : new ReProgmanSettings();
        }
        catch (IOException)
        {
            return new ReProgmanSettings();
        }
    }

    public static void Save(ReProgmanSettings settings)
    {
        try
        {
            File.WriteAllText(SettingsPath, settings.Save());
        }
        catch (IOException)
        {
            // A read-only install location silently loses settings, matching the
            // original PROGMAN.INI behavior on write-protected disks.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
