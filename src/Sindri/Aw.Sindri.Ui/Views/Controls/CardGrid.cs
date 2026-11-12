using Avalonia;
using Avalonia.Controls;

namespace Aw.Sindri.Ui.Views.Controls;

/// <summary>
/// Cards in rows that fill the width (#2905): as many columns of at least <see cref="MinColumnWidth"/> as fit, each stretched so a full
/// row ends on the panel's right edge, <see cref="Spacing"/> between cards and between rows.
/// Height rule: each card is arranged at its own measured height and is never stretched to a taller neighbour; a row advances by its
/// tallest card, and cards are measured again at the width they are arranged at, so wrapped text fits (#3117, #3101).
/// A child with <see cref="FullRowProperty"/> set takes a row of its own at the full width.
/// A hidden child takes no column. Without a finite width (a horizontal scroller) it is one column at the minimum width.
/// </summary>
internal sealed class CardGrid : Panel
{
    /// <summary>The narrowest a column may be.</summary>
    internal static readonly StyledProperty<double> MinColumnWidthProperty = AvaloniaProperty.Register<CardGrid, double>(nameof(MinColumnWidth), 420);

    /// <summary>The space between cards, across and down.</summary>
    internal static readonly StyledProperty<double> SpacingProperty = AvaloniaProperty.Register<CardGrid, double>(nameof(Spacing), 12);

    /// <summary>Set on a child to give it a row of its own at the full width.</summary>
    public static readonly AttachedProperty<bool> FullRowProperty = AvaloniaProperty.RegisterAttached<CardGrid, Control, bool>("FullRow");

    static CardGrid()
    {
        AffectsMeasure<CardGrid>(MinColumnWidthProperty, SpacingProperty);
        _ = FullRowProperty.Changed.AddClassHandler<Control>((c, _) => (c.Parent as Control)?.InvalidateMeasure());
    }

    /// <summary>Gets whether <paramref name="control"/> takes a full-width row of its own.</summary>
    /// <param name="control">The card.</param>
    /// <returns>True for a full row.</returns>
    public static bool GetFullRow(Control control) => control.GetValue(FullRowProperty);

    /// <summary>Sets whether <paramref name="control"/> takes a full-width row of its own.</summary>
    /// <param name="control">The card.</param>
    /// <param name="value">True for a full row.</param>
    public static void SetFullRow(Control control, bool value) => control.SetValue(FullRowProperty, value);

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
        var count = 0;
        foreach (var row in Rows(columns))
        {
            var tallest = 0.0;
            foreach (var child in row)
            {
                child.Measure(new Size(row.Count == 1 && GetFullRow(child) ? width : column, double.PositiveInfinity));
                tallest = Math.Max(tallest, child.DesiredSize.Height);
            }

            height += tallest + (count++ == 0 ? 0 : Spacing);
        }

        return new Size(count == 0 ? 0 : width, height);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var (columns, column, width) = Layout(finalSize.Width);
        var y = 0.0;
        foreach (var row in Rows(columns))
        {
            var tallest = 0.0;
            for (var i = 0; i < row.Count; i++)
            {
                var full = row.Count == 1 && GetFullRow(row[i]);
                var cardWidth = full ? width : column;

                // Measured again at the width it is arranged at, so wrapped text fits (#3101); a no-op when the width is unchanged.
                row[i].Measure(new Size(cardWidth, double.PositiveInfinity));
                var height = row[i].DesiredSize.Height;
                row[i].Arrange(new Rect(i * (column + Spacing), y, cardWidth, height));
                tallest = Math.Max(tallest, height);
            }

            y += tallest + Spacing;
        }

        return finalSize;
    }

    private List<List<Control>> Rows(int columns)
    {
        var rows = new List<List<Control>>();
        List<Control>? current = null;
        foreach (var child in Children.Where(c => c.IsVisible))
        {
            if (GetFullRow(child))
            {
                rows.Add([child]);
                current = null;
                continue;
            }

            if (current is null || current.Count == columns)
            {
                current = [];
                rows.Add(current);
            }

            current.Add(child);
        }

        return rows;
    }

    private (int Columns, double Column, double Width) Layout(double available)
    {
        var width = double.IsFinite(available) ? available : MinColumnWidth;
        var columns = ColumnsFor(width, MinColumnWidth, Spacing);
        return (columns, ColumnWidth(width, columns, Spacing), width);
    }
}
