using Avalonia;
using Avalonia.Controls;

namespace ReProgman.Controls;

/// <summary>
/// A Canvas whose desired size is the bounding box of its children, so that a
/// surrounding ScrollViewer shows scroll bars when icons sit outside the view.
/// </summary>
public class IconCanvas : Canvas
{
    protected override Size MeasureOverride(Size availableSize)
    {
        base.MeasureOverride(availableSize);

        double width = 0;
        double height = 0;
        foreach (var child in Children)
        {
            var x = GetLeft(child);
            var y = GetTop(child);
            width = Math.Max(width, (double.IsNaN(x) ? 0 : x) + child.DesiredSize.Width);
            height = Math.Max(height, (double.IsNaN(y) ? 0 : y) + child.DesiredSize.Height);
        }

        return new Size(width, height);
    }
}
