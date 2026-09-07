namespace ReProgman.Model;

/// <summary>A program group shown as one MDI child window, mirroring a Start Menu folder.</summary>
public sealed record ProgramGroup(string Name, IReadOnlyList<ProgramItem> Items);

/// <summary>A launchable shortcut inside a group.</summary>
public sealed record ProgramItem(string Name, string Path);
