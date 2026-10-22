using Avalonia;
using Avalonia.Controls;

namespace Aw.Sindri.Ui.Views.Workflows;

/// <summary>
/// The board's lane row: the lanes side by side with a 10 px gap, the Done lane a little narrower (star 0.6), each lane no narrower
/// than Size.LaneMin (Size.LaneMinLast for Done). The board gives the grid its height.
/// </summary>
#pragma warning disable MA0182 // placed by BoardView.axaml, which the analyzer does not read
internal sealed class LaneGrid : Grid
{
    private const double Gap = 10;

    protected override Size MeasureOverride(Size availableSize)
    {
        Columns();
        return base.MeasureOverride(availableSize);
    }

    // One column per lane, with a gap column between: the lanes are laid out at their index, so a lane added or removed never leaves a stale column.
    private void Columns()
    {
        var lanes = Children.Count;
        if (ColumnDefinitions.Count == (lanes * 2) - 1)
        {
            return;
        }

        var min = (double)(this.FindResource("Size.LaneMin") ?? 200d);
        var minLast = (double)(this.FindResource("Size.LaneMinLast") ?? 150d);
        ColumnDefinitions.Clear();
        RowDefinitions.Clear();
        RowDefinitions.Add(new RowDefinition(GridLength.Star));
        for (var i = 0; i < lanes; i++)
        {
            var last = i == lanes - 1;
            ColumnDefinitions.Add(new ColumnDefinition(new GridLength(last ? 0.6 : 1, GridUnitType.Star)) { MinWidth = last ? minLast : min });
            _ = Children[i].SetValue(ColumnProperty, i * 2);
            if (!last)
            {
                ColumnDefinitions.Add(new ColumnDefinition(new GridLength(Gap)));
            }
        }
    }
}
