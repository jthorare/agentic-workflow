using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Rows;

namespace Aw.Sindri.Ui.Views.Controls.Rows;

/// <summary>
/// A virtualized list of grouped rows with sticky group headers and one shared set of fact columns (#2978). ItemsSource holds <see cref="IRowGroup"/>s of
/// <see cref="IRowItem"/>; the list flattens them into one run of header and row entries over a virtualizing panel, so only the rows in view are built. It has no key
/// handler of its own: it listens for <see cref="RowKeys"/>' routed events and raises <see cref="PeekRequested"/>, <see cref="OpenRequested"/>, <see cref="ActionsRequested"/> (the `.` key: RowKeys handles it on the way down, before the shell sees it, so a page opens the cursor row's actions from this event) and <see cref="BackRequested"/>.
/// </summary>
[SuppressMessage("Design", "MA0182", Justification = "Pages adopt it in their own PRs (#2978).")]
internal sealed partial class RowList : ItemsControl, IRowKeyTarget
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

    private readonly RowRun _run;
    private INotifyCollectionChanged? _outer;
    private bool _attached;
    private ScrollViewer? _scroll;
    private RowGroupHeader? _sticky;

    static RowList()
    {
        FocusableProperty.OverrideDefaultValue<RowList>(defaultValue: true);   // so Esc and Space reach the page when no row has the cursor (an empty list)
        _ = ItemsSourceProperty.Changed.AddClassHandler<RowList>((list, _) => list.Rebuild());
        _ = ColumnsProperty.Changed.AddClassHandler<RowList>((list, _) => list.ForEachRow(list.Prepare));
        _ = PeekColumnsProperty.Changed.AddClassHandler<RowList>((list, _) => list.ForEachRow(list.Prepare));
        _ = IsPeekOpenProperty.Changed.AddClassHandler<RowList>((list, _) =>
        {
            list.ForEachRow(list.Prepare);
            if (!list.IsPeekOpen)
            {
                list.ReturnFocusToSelected();
            }
        });
        _ = SelectedItemProperty.Changed.AddClassHandler<RowList>((list, _) => list.ForEachRow(list.Prepare));
    }

    public RowList()
    {
        RowKeys.SetEnabled(this, value: true);
        KeyboardNavigation.SetTabNavigation(this, KeyboardNavigationMode.Once);   // the list is one tab stop; j/k and the arrows walk the rows
        AddHandler(RowKeys.PeekRequestedEvent, (_, e) => RaisePeek(new RowRequestEventArgs { Item = ItemAt(e.Index), Hold = e.Hold, Released = e.Released }));
        AddHandler(RowKeys.OpenRequestedEvent, (_, e) => RaiseOpen(new RowRequestEventArgs { Item = ItemAt(e.Index) }));
        AddHandler(RowKeys.ActionsRequestedEvent, (_, e) => RaiseActions(new RowRequestEventArgs { Item = ItemAt(e.Index) }));
        AddHandler(RowKeys.BackRequestedEvent, (_, e) => RaiseBack(new RowRequestEventArgs { Item = ItemAt(e.Index) }));
        AddHandler(GotFocusEvent, (_, e) =>
        {
            OnRowFocused(e.Source);
            PostStickyFocusSync();
        });
        AddHandler(LostFocusEvent, (_, _) => PostStickyFocusSync());
        AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
        AddHandler(TappedEvent, OnTapped);
        AddHandler(DoubleTappedEvent, OnDoubleTapped);
        _run = new RowRun(OnItemsChanged, OnCollapsed);
        ((ItemsControl)this).ItemsSource = _run.Entries;
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

    /// <summary>The row being peeked or open: an <see cref="IRowItem"/> or its key. A row is selected when its <see cref="IRowItem.Key"/> matches, so a poll that swaps in fresh objects keeps the selection.</summary>
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

    /// <summary>How many row containers are in use now (group headers and recycled containers not counted).</summary>
    internal int RealizedRowCount => ItemsPanelRoot?.Children.OfType<Row>().Count(r => r.IsVisible && r.DataContext is not null) ?? 0;

    /// <summary>How many row containers were ever made, counting recycled ones once.</summary>
    internal int ContainersCreated { get; private set; }

    /// <summary>How many times the panel has asked the list to fill a container (a header or a row) with an entry.</summary>
    internal int ContainersPrepared { get; private set; }

    /// <summary>The fixed row height now, 32 px or 40 px when an ancestor has class comfortable (the grammar puts it on .app).</summary>
    internal double RowHeight => HeightOf(IsComfortable() ? "Row.Height.Comfortable" : "Row.Height.Default", IsComfortable() ? 40 : 32);

    /// <summary>Raised when the cursor row should open or close its peek.</summary>
    public event EventHandler<RowRequestEventArgs>? PeekRequested;

    /// <summary>Raised when the cursor row should open in full.</summary>
    public event EventHandler<RowRequestEventArgs>? OpenRequested;

    /// <summary>Raised when Esc should step back: close the peek first, then leave the full view.</summary>
    public event EventHandler<RowRequestEventArgs>? BackRequested;

    /// <summary>Scrolls the list to a vertical offset; the sticky header follows.</summary>
    /// <param name="offset">The vertical offset in px.</param>
    internal void ScrollTo(double offset) => _scroll?.Offset = new Vector(0, Math.Max(0, offset));

    /// <inheritdoc />
    public bool IsRowIndex(int index) => index >= 0 && index < ItemCount && Items[index] is not GroupEntry;

    // The row at an index; null for a group header, so a key pressed on a header reaches the page without an item.
    private object? ItemAt(int index) => index >= 0 && index < ItemCount && Items[index] is not GroupEntry ? Items[index] : null;

    // While the peek is open, the cursor moving to another row moves the peek with it (j/k, arrows, a click).
    private void OnRowFocused(object? source)
    {
        if (IsPeekOpen && (source as Visual)?.FindAncestorOfType<Row>(includeSelf: true)?.DataContext is { } item && !IsSelected(item))
        {
            RaisePeek(new RowRequestEventArgs { Item = item, Follow = true });
        }
    }

    internal void RaisePeek(RowRequestEventArgs e) => PeekRequested?.Invoke(this, e);

    internal void RaiseOpen(RowRequestEventArgs e) => OpenRequested?.Invoke(this, e);

    internal void RaiseBack(RowRequestEventArgs e) => BackRequested?.Invoke(this, e);

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        Rebuild();   // subscribes, and catches what changed while the list was away
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        _outer?.CollectionChanged -= OnGroupsChanged;
        _run.Unsubscribe();
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        ArgumentNullException.ThrowIfNull(e);
        _scroll?.ScrollChanged -= OnScrollChanged;

        _scroll = e.NameScope.Find<ScrollViewer>("PART_ScrollViewer");
        _sticky = e.NameScope.Find<RowGroupHeader>("PART_Sticky");
        _sticky?.Focusable = false;   // the copy is a picture of the real header: never a tab stop or a click target for focus
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
        ContainersPrepared++;
        if (container is RowGroupHeader header && item is GroupEntry entry)
        {
            header.Margin = index == 0 ? default : new Thickness(0, GroupGap, 0, 0);
            header.Fill(entry.Group);
        }
        else if (container is Row row)
        {
            row.DataContext = item;
            Prepare(row);
            SetPlace(row, index);
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
        else if (container is RowGroupHeader header)
        {
            header.Fill(group: null);
        }

        base.ClearContainerForItemOverride(container);
    }

    private void Prepare(Row row)
    {
        row.Columns = Columns;
        row.VisibleColumns = IsPeekOpen ? PeekColumns : int.MaxValue;
        row.Classes.Set("sel", IsSelected(row.DataContext));
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

    private bool IsSelected(object? item) => SelectedItem is { } selected && item is not null && (ReferenceEquals(item, selected) || (RowRun.KeyOf(item) is { } key && string.Equals(key, RowRun.KeyOf(selected), StringComparison.Ordinal)));

    private bool IsComfortable() => this.GetSelfAndLogicalAncestors().OfType<StyledElement>().Any(a => a.Classes.Contains("comfortable"));

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e) => UpdateSticky();
}
