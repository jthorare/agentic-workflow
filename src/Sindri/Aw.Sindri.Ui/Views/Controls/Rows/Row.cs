using Avalonia;
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
internal sealed class Row : TemplatedControl
{
    /// <summary>The fact columns of the list, in order.</summary>
    public static readonly StyledProperty<IReadOnlyList<RowColumn>?> ColumnsProperty = AvaloniaProperty.Register<Row, IReadOnlyList<RowColumn>?>(nameof(Columns));

    /// <summary>How many of the fact columns show; the list lowers it while the peek is open.</summary>
    public static readonly StyledProperty<int> VisibleColumnsProperty = AvaloniaProperty.Register<Row, int>(nameof(VisibleColumns), defaultValue: int.MaxValue);

    private const double Gap = 12;

    private Parts? _parts;

    static Row()
    {
        FocusableProperty.OverrideDefaultValue<Row>(defaultValue: true);
        AffectsMeasure<Row>(ColumnsProperty);
    }

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
        if (change.Property == DataContextProperty || change.Property == ColumnsProperty)
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

    private void Refresh()
    {
        if (_parts is not { } p || DataContext is not IRowItem item)
        {
            return;
        }

        p.Glyph.Glyph = RowGlyphs.For(item.Kind, item.StateClass, item.StateKey);
        p.Id.Text = item.Id;
        p.Title.Text = item.Title;
        p.Title.FontWeight = item.Unread ? FontWeight.Medium : FontWeight.Normal;
        _ = p.Title.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable(item.Unread ? "Brush.Ink" : "Brush.InkMuted"));
        p.Sub.Text = item.SubLine;
        p.Sub.IsVisible = !string.IsNullOrEmpty(item.SubLine);
        p.Age.Text = item.Age;
        FillWaitsOn(p.Wait, item.WaitsOn);
        FillFacts(p.Facts, item);
        FillActions(p.Actions, item);
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
        _ = cell.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable(you ? "Brush.Ink" : "Brush.InkMuted"));
    }

    private void FillFacts(Panel host, IRowItem item)
    {
        host.Children.Clear();
        foreach (var column in Columns ?? [])
        {
            var width = ColumnWidth(column);
            var cell = new Border { Name = "Fact_" + column.Key, Width = width, ClipToBounds = true, Margin = new Thickness(Gap, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            if (item.Facts.TryGetValue(column.Key, out var fact))
            {
                cell.Child = RowFactViews.Build(fact, width);
            }

            host.Children.Add(cell);
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
            p.Facts.Children[i].IsVisible = i < VisibleColumns;
        }
    }

    private static void FillActions(Panel host, IRowItem item)
    {
        host.Children.Clear();
        foreach (var action in item.Actions)
        {
            host.Children.Add(new Button { Content = action.Label, Command = action.Command, Margin = new Thickness(4, 0, 0, 0), Padding = new Thickness(8, 0), MinHeight = 22, VerticalAlignment = VerticalAlignment.Center });
        }
    }

    private double ColumnWidth(RowColumn column) => Resource(column.Wide ? "Row.Col.FactWide" : "Row.Col.Fact", column.Wide ? 136 : 104);

    private double Resource(string key, double fallback) => this.TryFindResource(key, ActualThemeVariant, out var v) && v is double d ? d : fallback;

    private sealed record Parts(RowGlyphView Glyph, TextBlock Id, TextBlock Title, TextBlock Sub, Panel Facts, TextBlock Age, TextBlock Wait, Panel Actions);
}
