using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Aw.Sindri.Ui.ViewModels.Rows;

namespace Aw.Sindri.Ui.Views.Controls.Rows;

/// <summary>
/// One row of a <see cref="RowList"/> (#2978): glyph, id and title, one cell per list column, age and who it waits on. Its data context is an
/// IRowItem. Classes: cursor (keyboard focus within), sel (the row being peeked or open). The template is in RowStyles.axaml; the cells are filled here from the item.
/// </summary>
internal sealed partial class Row : TemplatedControl
{
    /// <summary>The fact columns of the list, in order.</summary>
    public static readonly StyledProperty<IReadOnlyList<RowColumn>?> ColumnsProperty = AvaloniaProperty.Register<Row, IReadOnlyList<RowColumn>?>(nameof(Columns));

    /// <summary>How many of the fact columns show; the list lowers it while the peek is open.</summary>
    public static readonly StyledProperty<int> VisibleColumnsProperty = AvaloniaProperty.Register<Row, int>(nameof(VisibleColumns), defaultValue: int.MaxValue);

    private const double Gap = 12;

    private static readonly RowColumn[] _noColumns = [];

    private static long _cellsBuilt;

    private Parts? _parts;
    private IRowItem? _watched;
    private IBrush? _ink;
    private IBrush? _inkMuted;
    private double[] _widths = [];
    private object? _builtFor;
    private Avalonia.Styling.ThemeVariant? _variant;
    private double _captionSize;

    static Row()
    {
        FocusableProperty.OverrideDefaultValue<Row>(defaultValue: true);
        AffectsMeasure<Row>(ColumnsProperty);
    }

    /// <summary>How many cells (fact cells and action buttons) rows ever made. A recycle reuses them, so this grows with the containers made and not with the rows scrolled past.</summary>
    internal static long CellsBuilt => Interlocked.Read(ref _cellsBuilt);

    /// <summary>The fact columns of the list, in order.</summary>
    public IReadOnlyList<RowColumn>? Columns
    {
        get => GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    /// <summary>How many of the fact columns show.</summary>
    public int VisibleColumns
    {
        get => GetValue(VisibleColumnsProperty);
        set => SetValue(VisibleColumnsProperty, value);
    }

    public Row()
    {
        ActualThemeVariantChanged += (_, _) =>
        {
            if (TopLevel.GetTopLevel(this) is null || Equals(_variant, ActualThemeVariant))
            {
                return;   // detached, or a recycled row re-entering the tree under the theme it already drew with
            }

            _variant = ActualThemeVariant;
            _ink = _inkMuted = null;
            Refresh();
        };
        ResourcesChanged += (_, _) =>
        {
            if (TopLevel.GetTopLevel(this) is not null && this.TryFindResource("Type.Caption.Size", ActualThemeVariant, out var size) && size is double points && !points.Equals(_captionSize))
            {
                _captionSize = points;   // the text scale moved: refs are cut again for the new size
                Refresh();
            }
        };
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        ArgumentNullException.ThrowIfNull(e);
        _parts = new Parts(
            e.NameScope.Get<RowGlyphView>("PART_Glyph"),
            e.NameScope.Get<TextBlock>("PART_Id"),
            e.NameScope.Get<TextBlock>("PART_Title"),
            e.NameScope.Get<TextBlock>("PART_Sub"),
            e.NameScope.Get<Panel>("PART_Facts"),
            e.NameScope.Get<TextBlock>("PART_Age"),
            e.NameScope.Get<TextBlock>("PART_Wait"),
            e.NameScope.Get<Panel>("PART_Actions"));
        Refresh();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        ArgumentNullException.ThrowIfNull(change);
        if (change.Property == DataContextProperty)
        {
            Watch(DataContext as IRowItem);
            Refresh();
        }
        else if (change.Property == ColumnsProperty)
        {
            Refresh();
        }
        else if (change.Property == VisibleColumnsProperty)
        {
            ShowColumns();
        }
        else if (change.Property == IsKeyboardFocusWithinProperty)
        {
            Classes.Set("cursor", IsKeyboardFocusWithin);
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Watch(DataContext as IRowItem);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Watch(item: null);
    }

    // The row follows its item: a changed title, age or fact redraws the text in place (IRowItem's doc).
    private void Watch(IRowItem? item)
    {
        var wanted = item is not null && TopLevel.GetTopLevel(this) is not null ? item : null;
        if (ReferenceEquals(_watched, wanted))
        {
            return;
        }

        _watched?.PropertyChanged -= OnItemChanged;
        _watched = wanted;
        _watched?.PropertyChanged += OnItemChanged;
    }

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    private void Refresh()
    {
        if (_parts is not { } p || DataContext is not IRowItem item)
        {
            return;
        }

        var glyph = RowGlyphs.For(item.Kind, item.StateClass, item.StateKey);
        if (p.Glyph.Glyph != glyph)
        {
            p.Glyph.Glyph = glyph;
        }

        var state = StateWord(item);
        AutomationProperties.SetName(p.Glyph, state);
        AutomationProperties.SetName(this, SpokenName(item, state));
        p.Id.Text = item.Id;
        p.Title.Text = item.Title;
        p.Title.FontWeight = item.Unread ? FontWeight.Medium : FontWeight.Normal;
        p.Title.Foreground = item.Unread ? Ink() : InkMuted();
        p.Sub.Text = item.SubLine;
        p.Sub.IsVisible = !string.IsNullOrEmpty(item.SubLine);
        p.Age.Text = item.Age;
        FillWaitsOn(p.Wait, item.WaitsOn);
        FillFacts(p.Facts, item);
        FillActions(p.Actions, item);
    }

    // "<state>, <title>, <facts>, waits on <who>, <age>": what a screen reader says for the row.
    private static string StateWord(IRowItem item) => (item.StateKey ?? item.StateClass.ToString()).Replace('-', ' ').ToLowerInvariant();

    private static string SpokenName(IRowItem item, string state)
    {
        var parts = new List<string> { state, item.Title };
        parts.AddRange(item.Facts.Values.Select(f => f.Text).Where(t => !string.IsNullOrEmpty(t)));
        if (item.WaitsOn is { } waits && waits.Who != RowWho.Nobody)
        {
            parts.Add("waits on " + (waits.Who == RowWho.Me ? "you" : waits.Name));
        }

        if (!string.IsNullOrEmpty(item.Age))
        {
            parts.Add(item.Age);
        }

        return string.Join(", ", parts);
    }

    private void FillWaitsOn(TextBlock cell, RowWaitsOn? waits)
    {
        if (waits is null || waits.Who == RowWho.Nobody)
        {
            cell.Text = string.Empty;
            return;
        }

        var you = waits.Who == RowWho.Me;
        cell.Text = you ? "you" : waits.Name;
        cell.FontWeight = you ? FontWeight.Bold : FontWeight.Normal;
        cell.Foreground = you ? Ink() : InkMuted();
    }

    // The cells (a Border per column) are made once per Row, when the columns are set; a recycle sets the text and a few properties on them (#2978 review 11).
    private void FillFacts(Panel host, IRowItem item)
    {
        var columns = Columns ?? _noColumns;
        if (!ReferenceEquals(_builtFor, columns) || host.Children.Count != columns.Count || _widths.Length != columns.Count)
        {
            _builtFor = columns;
            host.Children.Clear();
            _widths = [.. columns.Select(ColumnWidth)];
            for (var i = 0; i < columns.Count; i++)
            {
                host.Children.Add(new Border { Name = "Fact_" + columns[i].Key, Width = _widths[i], ClipToBounds = true, Margin = new Thickness(Gap, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
                _ = Interlocked.Increment(ref _cellsBuilt);
            }
        }

        for (var i = 0; i < columns.Count; i++)
        {
            var cell = (Border)host.Children[i];
            RowFactViews.Update(cell, item.Facts.TryGetValue(columns[i].Key, out var fact) ? fact : null, _widths[i]);
        }

        ShowColumns();
    }

    private void ShowColumns()
    {
        if (_parts is not { } p)
        {
            return;
        }

        for (var i = 0; i < p.Facts.Children.Count; i++)
        {
            var visible = i < VisibleColumns;
            if (p.Facts.Children[i].IsVisible != visible)
            {
                p.Facts.Children[i].IsVisible = visible;
            }
        }
    }

    private static void FillActions(Panel host, IRowItem item)
    {
        var actions = item.Actions;
        while (host.Children.Count > actions.Count)
        {
            host.Children.RemoveAt(host.Children.Count - 1);
        }

        for (var i = 0; i < actions.Count; i++)
        {
            if (i >= host.Children.Count)
            {
                host.Children.Add(new Button { Margin = new Thickness(4, 0, 0, 0), Padding = new Thickness(8, 0), MinHeight = 22, VerticalAlignment = VerticalAlignment.Center });
                _ = Interlocked.Increment(ref _cellsBuilt);
            }

            var button = (Button)host.Children[i];
            button.Content = actions[i].Label;
            button.Command = actions[i].Command;
        }
    }

    private IBrush? Ink() => _ink ??= Brush("Brush.Ink");

    private IBrush? InkMuted() => _inkMuted ??= Brush("Brush.InkMuted");

    private IBrush? Brush(string key) => this.TryFindResource(key, ActualThemeVariant, out var v) ? v as IBrush : null;

    private double ColumnWidth(RowColumn column) => Resource(column.Wide ? "Row.Col.FactWide" : "Row.Col.Fact", column.Wide ? 136 : 104);

    private double Resource(string key, double fallback) => this.TryFindResource(key, ActualThemeVariant, out var v) && v is double d ? d : fallback;

    private sealed record Parts(RowGlyphView Glyph, TextBlock Id, TextBlock Title, TextBlock Sub, Panel Facts, TextBlock Age, TextBlock Wait, Panel Actions);
}
