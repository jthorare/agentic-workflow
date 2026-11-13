using Avalonia;
using Avalonia.Controls;

namespace Aw.Sindri.Ui.Views.Controls;

/// <summary>
/// Cards in rows that fill the width (#2905): as many columns of at least <see cref="MinColumnWidth"/> as fit, each stretched so a full
/// row ends on the panel's right edge, <see cref="Spacing"/> between cards and between rows.
/// Height rule: each card is arranged at its own measured height and is never stretched to a taller neighbour; a row advances by its
/// tallest card (with ColumnMajor each column stacks on its own instead, so a tall card leaves no gap beside it), and cards are measured again at the width they are arranged at, so wrapped text fits (#3117, #3101).
/// A child with <see cref="FullRowProperty"/> set takes a row of its own at the full width, and splits a run of cards in two.
/// A hidden child takes no column. Without a finite width (a horizontal scroller) it is one column at the minimum width.
/// </summary>
internal sealed class CardGrid : Panel
{
    /// <summary>The narrowest a column may be.</summary>
    internal static readonly StyledProperty<double> MinColumnWidthProperty = AvaloniaProperty.Register<CardGrid, double>(nameof(MinColumnWidth), 420);

    /// <summary>The space between cards, across and down.</summary>
    internal static readonly StyledProperty<double> SpacingProperty = AvaloniaProperty.Register<CardGrid, double>(nameof(Spacing), 12);

    /// <summary>Whether a run of cards fills down each column before the next (the list reads down the left column, then down the right); off, it fills row by row. Children order is untouched, so Tab follows the reading order.</summary>
    internal static readonly StyledProperty<bool> ColumnMajorProperty = AvaloniaProperty.Register<CardGrid, bool>(nameof(ColumnMajor));

    /// <summary>Set on a child to give it a row of its own at the full width.</summary>
    public static readonly AttachedProperty<bool> FullRowProperty = AvaloniaProperty.RegisterAttached<CardGrid, Control, bool>("FullRow");

    private double? _arrangedWidth;   // the width the last arrange got when its stacked height differed from the measured one (#3355)
    private double _settledFor = double.NaN;   // the arrange width already re-measured for: the guard that lets the loop settle
    private double _lastAvailable = double.NaN;   // the width the parent last offered; a different one drops the two above

    static CardGrid()
    {
        AffectsMeasure<CardGrid>(MinColumnWidthProperty, SpacingProperty, ColumnMajorProperty);
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

    /// <summary>Gets or sets a value indicating whether a run of cards fills down each column before the next.</summary>
    internal bool ColumnMajor
    {
        get => GetValue(ColumnMajorProperty);
        set => SetValue(ColumnMajorProperty, value);
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
        if (availableSize.Width != _lastAvailable)
        {
            _lastAvailable = availableSize.Width;
            _arrangedWidth = null;
            _settledFor = double.NaN;
        }

        // The parent offers one width and arranges at another: measure at the width we are drawn at, but answer with the width offered.
        var (_, _, offered) = Layout(availableSize.Width);
        var measureWidth = _arrangedWidth is { } arranged && arranged < availableSize.Width ? arranged : availableSize.Width;
        var (columns, column, width) = Layout(measureWidth);
        var (height, any) = Flow(columns, column, width, arrange: false);
        return new Size(any ? offered : 0, height);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var (columns, column, width) = Layout(finalSize.Width);
        var (stacked, _) = Flow(columns, column, width, arrange: true);

        // The cards took more or less room at this width than at the measured one: measure again at it, once per width (#3355).
        if (Math.Abs(stacked - DesiredSize.Height) > 0.5 && finalSize.Width != _settledFor)
        {
            _arrangedWidth = finalSize.Width;
            _settledFor = finalSize.Width;
            InvalidateMeasure();
        }

        return finalSize;
    }

    // Measures every card at its width and, when arranging, places it. Row-major rows are paired: a row is as tall as its tallest card. Column-major stacks each column on its own, so a tall
    // card leaves no gap beside it (#3493); a full-row card starts below the taller column. Returns the stacked height (no trailing gap) and whether there was any card.
    private (double Height, bool Any) Flow(int columns, double column, double width, bool arrange)
    {
        var tops = new double[columns];
        var any = false;
        foreach (var row in Rows(columns))
        {
            any = true;
            var full = row.Count == 1 && GetFullRow(row[0]);
            var rowTop = tops.Max();
            var tallest = PlaceRow(row, new Pass(column, width, arrange), full, tops, rowTop);
            if (!ColumnMajor || full)
            {
                Array.Fill(tops, rowTop + tallest + Spacing);
            }
        }

        return (any ? tops.Max() - Spacing : 0, any);
    }

    private readonly record struct Pass(double Column, double Width, bool Arrange);

    // One row: each card measured at its width (again at the width it is arranged at, so wrapped text fits, #3101; a no-op when unchanged) and placed at its column's top (column-major) or the row's top.
    private double PlaceRow(List<Control> row, Pass pass, bool full, double[] tops, double rowTop)
    {
        var cardWidth = full ? pass.Width : pass.Column;
        var tallest = 0.0;
        for (var i = 0; i < row.Count; i++)
        {
            row[i].Measure(new Size(cardWidth, double.PositiveInfinity));
            var height = row[i].DesiredSize.Height;
            var y = ColumnMajor && !full ? tops[i] : rowTop;
            if (pass.Arrange)
            {
                row[i].Arrange(new Rect(i * (pass.Column + Spacing), y, cardWidth, height));
            }

            tops[i] = y + height + Spacing;
            tallest = Math.Max(tallest, height);
        }

        return tallest;
    }

    /// <summary>Which children go in which cell: each row is the child indexes left to right. A run of cards between full-row children fills row by row, or, with <paramref name="columnMajor"/>, column by column (the first ceil(n / columns) down the left column).</summary>
    /// <param name="full">Whether each visible child takes a full row.</param>
    /// <param name="columns">The column count.</param>
    /// <param name="columnMajor">True to fill down each column before the next.</param>
    /// <returns>The rows.</returns>
    internal static List<int[]> Place(IReadOnlyList<bool> full, int columns, bool columnMajor)
    {
        var rows = new List<int[]>();
        var run = new List<int>();
        for (var i = 0; i < full.Count; i++)
        {
            if (!full[i])
            {
                run.Add(i);
                continue;
            }

            AddRun(rows, run, columns, columnMajor);
            rows.Add([i]);
        }

        AddRun(rows, run, columns, columnMajor);
        return rows;
    }

    // A run of cards as rows: row by row, or down each column in turn (column c holds run[c * per .. (c + 1) * per), per = ceil(n / columns)). Only the trailing cells of a row can be empty.
    private static void AddRun(List<int[]> rows, List<int> run, int columns, bool columnMajor)
    {
        if (run.Count == 0)
        {
            return;
        }

        if (columnMajor)
        {
            var per = (run.Count + columns - 1) / columns;
            for (var r = 0; r < per; r++)
            {
                rows.Add([.. Enumerable.Range(0, columns).Select(c => (c * per) + r).Where(at => at < run.Count).Select(at => run[at])]);
            }
        }
        else
        {
            for (var i = 0; i < run.Count; i += columns)
            {
                rows.Add([.. run.Skip(i).Take(columns)]);
            }
        }

        run.Clear();
    }

    private List<List<Control>> Rows(int columns)
    {
        var visible = Children.Where(c => c.IsVisible).ToList();
        return [.. Place([.. visible.Select(GetFullRow)], columns, ColumnMajor).Select(row => row.Select(i => visible[i]).ToList())];
    }

    private (int Columns, double Column, double Width) Layout(double available)
    {
        var width = double.IsFinite(available) ? available : MinColumnWidth;
        var columns = ColumnsFor(width, MinColumnWidth, Spacing);
        return (columns, ColumnWidth(width, columns, Spacing), width);
    }
}
