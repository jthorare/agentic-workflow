using Avalonia;
using Avalonia.Controls;

namespace Aw.Sindri.Ui.Views.Controls;

/// <summary>
/// Cards in rows that fill the width (#2905): as many columns of at least <see cref="MinColumnWidth"/> as fit, each stretched so a full
/// row ends on the panel's right edge, <see cref="Spacing"/> between cards and between rows, and each row as tall as its tallest card.
/// A hidden child takes no column. Without a finite width (a horizontal scroller) it is one column at the minimum width.
/// </summary>
internal sealed class CardGrid : Panel
{
    /// <summary>The narrowest a column may be.</summary>
    internal static readonly StyledProperty<double> MinColumnWidthProperty = AvaloniaProperty.Register<CardGrid, double>(nameof(MinColumnWidth), 420);

    /// <summary>The space between cards, across and down.</summary>
    internal static readonly StyledProperty<double> SpacingProperty = AvaloniaProperty.Register<CardGrid, double>(nameof(Spacing), 12);

    static CardGrid()
    {
        AffectsMeasure<CardGrid>(MinColumnWidthProperty, SpacingProperty);
    }

    /// <summary>Gets or sets the narrowest a column may be.</summary>
    internal double MinColumnWidth
    {
        get => GetValue(MinColumnWidthProperty);
        set => SetValue(MinColumnWidthProperty, value);
    }

    /// <summary>Gets or sets the space between cards, across and down.</summary>
    internal double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    /// <summary>How many columns of at least <paramref name="minColumn"/> fit in <paramref name="width"/> with <paramref name="gap"/> between them; at least one.</summary>
    /// <param name="width">The width to fill.</param>
    /// <param name="minColumn">The narrowest a column may be.</param>
    /// <param name="gap">The space between columns.</param>
    /// <returns>The column count.</returns>
    internal static int ColumnsFor(double width, double minColumn, double gap) =>
        double.IsFinite(width) && minColumn > 0 ? Math.Max(1, (int)Math.Floor((width + gap) / (minColumn + gap))) : 1;

    /// <summary>One column's width when <paramref name="columns"/> columns and their gaps fill <paramref name="width"/>.</summary>
    /// <param name="width">The width to fill.</param>
    /// <param name="columns">The column count.</param>
    /// <param name="gap">The space between columns.</param>
    /// <returns>The column width.</returns>
    internal static double ColumnWidth(double width, int columns, double gap) => Math.Max(0, (width - (gap * (columns - 1))) / columns);

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var (columns, column, width) = Layout(availableSize.Width);
        var height = 0.0;
        var row = 0.0;
        var index = 0;
        foreach (var child in Children.Where(c => c.IsVisible))
        {
            child.Measure(new Size(column, double.PositiveInfinity));
            row = Math.Max(row, child.DesiredSize.Height);
            if (++index % columns == 0)
            {
                height += row + Spacing;
                row = 0;
            }
        }

        height = index == 0 ? 0 : index % columns == 0 ? height - Spacing : height + row;
        return new Size(index == 0 ? 0 : width, height);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var (columns, column, _) = Layout(finalSize.Width);
        var visible = Children.Where(c => c.IsVisible).ToList();
        var y = 0.0;
        for (var start = 0; start < visible.Count; start += columns)
        {
            var cards = visible.Skip(start).Take(columns).ToList();
            var row = cards.Max(c => c.DesiredSize.Height);
            for (var i = 0; i < cards.Count; i++)
            {
                cards[i].Arrange(new Rect(i * (column + Spacing), y, column, row));
            }

            y += row + Spacing;
        }

        return finalSize;
    }

    private (int Columns, double Column, double Width) Layout(double available)
    {
        var width = double.IsFinite(available) ? available : MinColumnWidth;
        var columns = ColumnsFor(width, MinColumnWidth, Spacing);
        return (columns, ColumnWidth(width, columns, Spacing), width);
    }
}
