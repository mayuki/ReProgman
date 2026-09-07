using System.Globalization;

namespace ReProgman.Model;

public enum WindowStateKind
{
    Normal,
    Minimized,
    Maximized,
}

/// <summary>
/// A window position/size/state snapshot serialized as "x,y,width,height,state" in INI files.
/// </summary>
public readonly record struct WindowPlacement(int X, int Y, int Width, int Height, WindowStateKind State)
{
    public string ToIniString() => string.Create(
        CultureInfo.InvariantCulture,
        $"{X},{Y},{Width},{Height},{State.ToString().ToLowerInvariant()}");

    public static bool TryParse(string? text, out WindowPlacement placement)
    {
        placement = default;
        if (text is null)
        {
            return false;
        }

        var parts = text.Split(',');
        if (parts.Length != 5)
        {
            return false;
        }

        if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) ||
            !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y) ||
            !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var width) ||
            !int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var height) ||
            !Enum.TryParse<WindowStateKind>(parts[4].Trim(), ignoreCase: true, out var state))
        {
            return false;
        }

        placement = new WindowPlacement(x, y, width, height, state);
        return true;
    }
}
