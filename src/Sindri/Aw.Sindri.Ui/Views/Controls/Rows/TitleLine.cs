using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;

namespace Aw.Sindri.Ui.Views.Controls.Rows;

/// <summary>
/// The id, title and sub-line of a row on one line. The id and the sub-line keep their size; the title takes what is left and cuts with an ellipsis. The panel asks the
/// title for its full width, so its desired size says how much it would need, while it is arranged at the room there is.
/// </summary>
[SuppressMessage("Design", "MA0182", Justification = "Used by the Row template in RowStyles.axaml.")]
internal sealed class TitleLine : Panel
{
    private const double Gap = 6;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = 0d;
        var height = 0d;
        foreach (var child in Children.Where(c => c.IsVisible))
        {
            child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            width += child.DesiredSize.Width + (width > 0 ? Gap : 0);
            height = Math.Max(height, child.DesiredSize.Height);
        }

        return new Size(Math.Min(width, double.IsInfinity(availableSize.Width) ? width : availableSize.Width), height);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var visible = Children.Where(c => c.IsVisible).ToList();
        var fixedWidth = visible.Where((c, i) => i != 1 || visible.Count < 2).Sum(c => c.DesiredSize.Width) + (Gap * Math.Max(0, visible.Count - 1));
        var x = 0d;
        for (var i = 0; i < visible.Count; i++)
        {
            var child = visible[i];
            var flexes = i == 1;
            var room = flexes ? Math.Max(0, finalSize.Width - fixedWidth) : child.DesiredSize.Width;
            var width = flexes ? Math.Min(child.DesiredSize.Width, room) : Math.Min(room, Math.Max(0, finalSize.Width - x));
            child.Arrange(new Rect(x, (finalSize.Height - child.DesiredSize.Height) / 2, width, child.DesiredSize.Height));
            x += width + Gap;
        }

        return finalSize;
    }
}
