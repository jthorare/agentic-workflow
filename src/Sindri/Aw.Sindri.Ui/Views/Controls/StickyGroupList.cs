using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.ViewModels;

namespace Aw.Sindri.Ui.Views.Controls;

/// <summary>
/// A scrolling list of groups under sticky headers (Needs-you spec §2): each group is a header and its rows, and while a group's
/// rows are in view its header stays at the top, pushed out by the next group's header as it arrives. The groups' headers and rows
/// are laid out as ONE flat list in a virtualizing panel (#2776), so only the rows in view are built, however many groups and rows
/// there are. The flat list is patched in place when a group or a row comes or goes, so a row that stays keeps its controls and
/// focus. The sticky header follows the scroll and the layout: it is the header of the group of the first row in view.
/// </summary>
internal sealed partial class StickyGroupList : UserControl
{
    /// <summary>The groups, each an <see cref="IRowGroup"/>.</summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty = AvaloniaProperty.Register<StickyGroupList, IEnumerable?>(nameof(ItemsSource));

    /// <summary>How a group's header is drawn; its data context is the group.</summary>
    public static readonly StyledProperty<IDataTemplate?> HeaderTemplateProperty = AvaloniaProperty.Register<StickyGroupList, IDataTemplate?>(nameof(HeaderTemplate));

    /// <summary>How a row is drawn.</summary>
    public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty = AvaloniaProperty.Register<StickyGroupList, IDataTemplate?>(nameof(ItemTemplate));

    private readonly ScrollViewer _scroll = new() { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
    private readonly ItemsControl _items = new();
    private readonly ContentControl _sticky = new() { VerticalAlignment = VerticalAlignment.Top };
    private readonly TranslateTransform _push = new();
    private readonly ObservableCollection<object> _flat = [];
    private readonly List<object> _order = [];                       // the groups, in order
    private readonly List<object> _groupOf = [];                     // for each flat index, the group it belongs to
    private readonly Dictionary<object, int> _headerAt = new(ReferenceEqualityComparer.Instance);   // a group's header's flat index
    private readonly Dictionary<object, INotifyCollectionChanged> _watchedRows = new(ReferenceEqualityComparer.Instance);
    private INotifyCollectionChanged? _watched;

    public StickyGroupList()
    {
        _items.ItemsPanel = new FuncTemplate<Panel?>(() => new VirtualizingStackPanel());
        _items.ItemTemplate = new FlatTemplate(this);
        _items.ItemsSource = _flat;
        _scroll.Content = _items;
        _sticky.RenderTransform = _push;
        _sticky.Classes.Add("sticky-header");
        AutomationProperties.SetAccessibilityView(_sticky, AccessibilityView.Raw);   // the real header, below it, is what is read
        Content = new Panel { ClipToBounds = true, Children = { _scroll, _sticky } };
        _scroll.ScrollChanged += (_, _) => FollowScroll();
    }

    /// <summary>The groups.</summary>
    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <summary>How a group's header is drawn.</summary>
    public IDataTemplate? HeaderTemplate
    {
        get => GetValue(HeaderTemplateProperty);
        set => SetValue(HeaderTemplateProperty, value);
    }

    /// <summary>How a row is drawn.</summary>
    public IDataTemplate? ItemTemplate
    {
        get => GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    /// <summary>The group whose header the sticky header shows now; null while the list is at its top.</summary>
    internal object? StickyGroup => _sticky.Content;

    /// <summary>How far the sticky header is pushed up by the next group's header: 0 until the next header reaches it, then negative.</summary>
    internal double StickyPush => _push.Y;

    /// <summary>Scrolls a row fully into view below the sticky header (never under it).</summary>
    /// <param name="row">A row of the list.</param>
    internal static void EnsureVisible(Control row) => row.FindAncestorOfType<StickyGroupList>()?.Reveal(row);

    /// <summary>Scrolls the first row the test accepts into view and lays it out, so it is built when this returns true (a row out of view has no control until then).</summary>
    /// <param name="isRow">True for the wanted row (not a group).</param>
    /// <returns>True when a row matched and the list scrolled to it.</returns>
    internal bool ScrollToRow(Func<object, bool> isRow)
    {
        ArgumentNullException.ThrowIfNull(isRow);
        for (var i = 0; i < _flat.Count; i++)
        {
            if (_flat[i] is not IRowGroup && isRow(_flat[i]))
            {
                _items.ScrollIntoView(i);
                _scroll.UpdateLayout();
                return true;
            }
        }

        return false;
    }

    // The row's top clears the header height, its bottom the viewport's: the focus's own bring-into-view lines the row up with the viewport's top, under the header.
    // Positions are read against the scroll viewer, so they are the same whatever the virtualizing panel does with its own coordinates.
    private void Reveal(Control row)
    {
        if (row.TranslatePoint(default, _scroll) is not { } at)
        {
            return;
        }

        var header = HeaderHeight(Realized());
        var offset = _scroll.Offset.Y;
        if (at.Y < header)
        {
            ScrollTo(Math.Max(0, offset + at.Y - header));
        }
        else if (at.Y + row.Bounds.Height > _scroll.Viewport.Height)
        {
            ScrollTo(offset + at.Y + row.Bounds.Height - _scroll.Viewport.Height);
        }
    }

    /// <summary>Scrolls the list; the sticky header follows.</summary>
    /// <param name="offset">The vertical offset.</param>
    internal void ScrollTo(double offset) => _scroll.Offset = new Vector(0, offset);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty)
        {
            Watch(change.GetNewValue<IEnumerable?>());
            Sync();
        }
        else if (change.Property == HeaderTemplateProperty || change.Property == ItemTemplateProperty)
        {
            _sticky.ContentTemplate = HeaderTemplate;
            _items.ItemTemplate = new FlatTemplate(this);   // a new template instance makes the realized rows draw again
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        LayoutUpdated += OnLayoutUpdated;
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        LayoutUpdated -= OnLayoutUpdated;
        base.OnDetachedFromVisualTree(e);
    }

    // The panel builds the rows of a new offset during layout, so the header that sticks is worked out again after it.
    private void OnLayoutUpdated(object? sender, EventArgs e) => FollowScroll();

    private void Watch(IEnumerable? source)
    {
        _watched?.CollectionChanged -= OnGroupsChanged;
        _watched = source as INotifyCollectionChanged;
        _watched?.CollectionChanged += OnGroupsChanged;
    }

    private void OnGroupsChanged(object? sender, NotifyCollectionChangedEventArgs e) => Sync();

    private void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Rebuild();
        FollowScroll();
    }

    // Follows the rows of every group that stays, drops the ones that went, and lays the headers and rows out again in the groups' order.
    private void Sync()
    {
        var groups = ItemsSource?.Cast<object>().Where(g => g is IRowGroup).ToList() ?? [];
        foreach (var gone in _watchedRows.Keys.Where(k => !groups.Contains(k, ReferenceEqualityComparer.Instance)).ToList())
        {
            _watchedRows[gone].CollectionChanged -= OnRowsChanged;
            _ = _watchedRows.Remove(gone);
        }

        foreach (var group in groups.Where(g => !_watchedRows.ContainsKey(g)))
        {
            if (((IRowGroup)group).Items is INotifyCollectionChanged live)
            {
                live.CollectionChanged += OnRowsChanged;
                _watchedRows[group] = live;
            }
        }

        _order.Clear();
        _order.AddRange(groups);
        Rebuild();
        FollowScroll();
    }

    // The flat list is the headers and rows in order; it is patched, so the controls of what stays are kept.
    private void Rebuild()
    {
        var target = new List<object>();
        _groupOf.Clear();
        _headerAt.Clear();
        foreach (var group in _order)
        {
            _headerAt[group] = target.Count;
            target.Add(group);
            _groupOf.Add(group);
            foreach (var row in ((IRowGroup)group).Items.Cast<object>())
            {
                target.Add(row);
                _groupOf.Add(group);
            }
        }

        if (_flat.Count == 0)
        {
            _items.ItemsSource = null;   // a first fill is one change, not one per row
            foreach (var item in target)
            {
                _flat.Add(item);
            }

            _items.ItemsSource = _flat;
        }
        else
        {
            Patch.To(_flat, target);
        }
    }
}
