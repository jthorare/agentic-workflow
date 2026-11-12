using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Rows;

namespace Aw.Sindri.Ui.Views.Controls.Rows;

/// <summary>
/// A virtualized list of grouped rows with sticky group headers and one shared set of fact columns (#2978). ItemsSource holds <see cref="IRowGroup"/>s of
/// <see cref="IRowItem"/>; the list flattens them into one run of header and row entries over a virtualizing panel, so only the rows in view are built. It has no key
/// handler of its own: it listens for <see cref="RowKeys"/>' routed events and raises <see cref="PeekRequested"/>, <see cref="OpenRequested"/> and <see cref="BackRequested"/>.
/// </summary>
[SuppressMessage("Design", "MA0182", Justification = "Pages adopt it in their own PRs (#2978).")]
internal sealed class RowList : ItemsControl
{
    /// <summary>The groups, each an <see cref="IRowGroup"/>. Hides the base property: the base one holds the flat entries.</summary>
    public static new readonly StyledProperty<IEnumerable?> ItemsSourceProperty = AvaloniaProperty.Register<RowList, IEnumerable?>(nameof(ItemsSource));

    /// <summary>The fact columns every row shows, in order.</summary>
    public static readonly StyledProperty<IReadOnlyList<RowColumn>?> ColumnsProperty = AvaloniaProperty.Register<RowList, IReadOnlyList<RowColumn>?>(nameof(Columns));

    /// <summary>How many fact columns stay while the peek is open.</summary>
    public static readonly StyledProperty<int> PeekColumnsProperty = AvaloniaProperty.Register<RowList, int>(nameof(PeekColumns), defaultValue: 3);

    /// <summary>The row being peeked or open.</summary>
    public static readonly StyledProperty<object?> SelectedItemProperty = AvaloniaProperty.Register<RowList, object?>(nameof(SelectedItem));

    /// <summary>True while the page shows the peek pane beside the list.</summary>
    public static readonly StyledProperty<bool> IsPeekOpenProperty = AvaloniaProperty.Register<RowList, bool>(nameof(IsPeekOpen));

    private const string RowKey = "row";
    private const string HeaderKey = "header";

    private readonly List<INotifyCollectionChanged> _watched = [];
    private readonly List<double> _groupTops = [];
    private readonly List<IRowGroup> _groups = [];
    private ScrollViewer? _scroll;
    private RowGroupHeader? _sticky;

    static RowList()
    {
        _ = ItemsSourceProperty.Changed.AddClassHandler<RowList>((list, _) => list.Rebuild());
        _ = ColumnsProperty.Changed.AddClassHandler<RowList>((list, _) => list.ForEachRow(list.Prepare));
        _ = PeekColumnsProperty.Changed.AddClassHandler<RowList>((list, _) => list.ForEachRow(list.Prepare));
        _ = IsPeekOpenProperty.Changed.AddClassHandler<RowList>((list, _) => list.ForEachRow(list.Prepare));
        _ = SelectedItemProperty.Changed.AddClassHandler<RowList>((list, _) => list.ForEachRow(list.Prepare));
    }

    public RowList()
    {
        RowKeys.SetEnabled(this, value: true);
    }

    /// <summary>The groups, each an <see cref="IRowGroup"/>.</summary>
    public new IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <summary>The fact columns every row shows.</summary>
    public IReadOnlyList<RowColumn>? Columns
    {
        get => GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    /// <summary>How many fact columns stay while the peek is open.</summary>
    public int PeekColumns
    {
        get => GetValue(PeekColumnsProperty);
        set => SetValue(PeekColumnsProperty, value);
    }

    /// <summary>The row being peeked or open.</summary>
    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    /// <summary>True while the page shows the peek pane beside the list.</summary>
    public bool IsPeekOpen
    {
        get => GetValue(IsPeekOpenProperty);
        set => SetValue(IsPeekOpenProperty, value);
    }

    /// <summary>The group whose header the sticky header shows now; null while the list is at its top.</summary>
    internal object? StickyGroup => _sticky is { IsVisible: true } sticky ? sticky.Group : null;

    /// <summary>How far the sticky header is pushed up by the next group's header.</summary>
    internal double StickyPush => _sticky?.RenderTransform is TranslateTransform t ? t.Y : 0;

    /// <summary>How many row containers are in use now (group headers and recycled containers not counted).</summary>
    internal int RealizedRowCount => ItemsPanelRoot?.Children.OfType<Row>().Count(r => r.IsVisible && r.DataContext is not null) ?? 0;

    /// <summary>How many row containers were ever made, counting recycled ones once.</summary>
    internal int ContainersCreated { get; private set; }

    /// <summary>Raised when the cursor row should open or close its peek.</summary>
    public event EventHandler<RowRequestEventArgs>? PeekRequested;

    /// <summary>Raised when the cursor row should open in full.</summary>
    public event EventHandler<RowRequestEventArgs>? OpenRequested;

    /// <summary>Raised when Esc should step back: close the peek first, then leave the full view.</summary>
    public event EventHandler<RowRequestEventArgs>? BackRequested;

    /// <summary>Scrolls the list to a vertical offset; the sticky header follows.</summary>
    /// <param name="offset">The vertical offset in px.</param>
    internal void ScrollTo(double offset) => _scroll?.Offset = new Vector(0, Math.Max(0, offset));

    internal void RaisePeek(RowRequestEventArgs e) => PeekRequested?.Invoke(this, e);

    internal void RaiseOpen(RowRequestEventArgs e) => OpenRequested?.Invoke(this, e);

    internal void RaiseBack(RowRequestEventArgs e) => BackRequested?.Invoke(this, e);

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        ArgumentNullException.ThrowIfNull(e);
        _scroll?.ScrollChanged -= OnScrollChanged;

        _scroll = e.NameScope.Find<ScrollViewer>("PART_ScrollViewer");
        _sticky = e.NameScope.Find<RowGroupHeader>("PART_Sticky");
        _scroll?.ScrollChanged += OnScrollChanged;
    }

    /// <inheritdoc />
    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
    {
        recycleKey = item is GroupEntry ? HeaderKey : RowKey;
        return true;
    }

    /// <inheritdoc />
    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey)
    {
        ContainersCreated++;
        return item is GroupEntry ? new RowGroupHeader() : new Row();
    }

    /// <inheritdoc />
    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        ArgumentNullException.ThrowIfNull(container);
        base.PrepareContainerForItemOverride(container, item, index);
        if (container is RowGroupHeader header && item is GroupEntry entry)
        {
            header.Fill(entry.Group);
        }
        else if (container is Row row)
        {
            row.DataContext = item;
            Prepare(row);
        }
    }

    /// <inheritdoc />
    protected override void ClearContainerForItemOverride(Control container)
    {
        ArgumentNullException.ThrowIfNull(container);
        if (container is Row row)
        {
            row.Classes.Set("sel", value: false);
            row.DataContext = null;
        }

        base.ClearContainerForItemOverride(container);
    }

    private void Prepare(Row row)
    {
        row.Columns = Columns;
        row.VisibleColumns = IsPeekOpen ? PeekColumns : int.MaxValue;
        row.Classes.Set("sel", SelectedItem is not null && ReferenceEquals(row.DataContext, SelectedItem));
    }

    private void ForEachRow(Action<Row> action)
    {
        foreach (var row in ItemsPanelRoot?.Children.OfType<Row>() ?? [])
        {
            if (row.DataContext is not null)
            {
                action(row);
            }
        }
    }

    // Flattens the groups into header and row entries. A change in the groups or in any group's rows builds the run again; the rows in view are rebuilt, nothing else is.
    private void Rebuild()
    {
        foreach (var watched in _watched)
        {
            watched.CollectionChanged -= OnSourceChanged;
        }

        _watched.Clear();
        _groups.Clear();
        _groupTops.Clear();
        var entries = new List<object>();
        var rowHeight = HeightOf(Classes.Contains("comfortable") ? "Row.Height.Comfortable" : "Row.Height.Default", 32);
        var headerHeight = HeightOf("Row.GroupHeader", 28);
        var top = 0d;
        Watch(ItemsSource);
        foreach (var group in ItemsSource?.OfType<IRowGroup>() ?? [])
        {
            Watch(group.Items);
            _groups.Add(group);
            _groupTops.Add(top);
            entries.Add(new GroupEntry(group));
            var count = 0;
            foreach (var item in group.Items)
            {
                entries.Add(item);
                count++;
            }

            top += headerHeight + (count * rowHeight);
        }

        _ = SetValue(ItemsControl.ItemsSourceProperty, entries);
        UpdateSticky();
    }

    private void Watch(IEnumerable? source)
    {
        if (source is INotifyCollectionChanged changing)
        {
            changing.CollectionChanged += OnSourceChanged;
            _watched.Add(changing);
        }
    }

    private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e) => UpdateSticky();

    // The header that sticks is the last one whose group has reached the top; the next group's header pushes it out as it arrives.
    private void UpdateSticky()
    {
        if (_sticky is null || _scroll is null)
        {
            return;
        }

        var offset = _scroll.Offset.Y;
        if (_groups.Count == 0 || offset <= 0.5)
        {
            _sticky.IsVisible = false;
            return;
        }

        var active = 0;
        for (var i = 0; i < _groupTops.Count; i++)
        {
            if (_groupTops[i] <= offset + 0.5)
            {
                active = i;
            }
        }

        var next = active + 1 < _groupTops.Count ? _groupTops[active + 1] : double.PositiveInfinity;
        if (!ReferenceEquals(_sticky.Group, _groups[active]))
        {
            _sticky.Fill(_groups[active]);
        }

        _sticky.RenderTransform = new TranslateTransform(0, StickyHeader.PushOut(next, offset, HeightOf("Row.GroupHeader", 28)));
        _sticky.IsVisible = true;
    }

    private double HeightOf(string key, double fallback) => this.TryFindResource(key, ActualThemeVariant, out var v) && v is double d ? d : fallback;

    // The entry for a group header in the flat run.
    private sealed record GroupEntry(IRowGroup Group);
}
