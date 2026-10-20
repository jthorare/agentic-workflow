using System.Collections;
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
/// rows are in view its header stays at the top, pushed out by the next group's header as it arrives. The groups' rows are bound
/// live, so a row that comes or goes changes nothing else; the groups themselves are kept across a re-read, so a row that stays
/// keeps its controls and focus. Nothing is measured on every layout pass: the sticky header follows the scroll, and a group
/// change, never its own move.
/// </summary>
internal sealed class StickyGroupList : UserControl
{
    /// <summary>The groups, each an <see cref="IRowGroup"/>.</summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty = AvaloniaProperty.Register<StickyGroupList, IEnumerable?>(nameof(ItemsSource));

    /// <summary>How a group's header is drawn; its data context is the group.</summary>
    public static readonly StyledProperty<IDataTemplate?> HeaderTemplateProperty = AvaloniaProperty.Register<StickyGroupList, IDataTemplate?>(nameof(HeaderTemplate));

    /// <summary>How a row is drawn.</summary>
    public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty = AvaloniaProperty.Register<StickyGroupList, IDataTemplate?>(nameof(ItemTemplate));

    private readonly ScrollViewer _scroll = new() { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
    private readonly StackPanel _stack = new();
    private readonly ContentControl _sticky = new() { VerticalAlignment = VerticalAlignment.Top };
    private readonly TranslateTransform _push = new();
    private readonly Dictionary<object, Block> _blocks = new(ReferenceEqualityComparer.Instance);
    private readonly List<object> _order = [];
    private INotifyCollectionChanged? _watched;

    public StickyGroupList()
    {
        _scroll.Content = _stack;
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

    // The row's top clears the header height, its bottom the viewport's: the focus's own bring-into-view lines the row up with the viewport's top, under the header.
    private void Reveal(Control row)
    {
        if (row.TranslatePoint(default, _stack) is not { } at)
        {
            return;
        }

        var header = _order.Count > 0 ? _blocks[_order[0]].Header.Bounds.Height : 0;
        var offset = _scroll.Offset.Y;
        if (at.Y < offset + header)
        {
            ScrollTo(Math.Max(0, at.Y - header));
        }
        else if (at.Y + row.Bounds.Height > offset + _scroll.Viewport.Height)
        {
            ScrollTo(at.Y + row.Bounds.Height - _scroll.Viewport.Height);
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
            foreach (var block in _blocks.Values)
            {
                block.Apply(HeaderTemplate, ItemTemplate);
            }
        }
    }

    private void Watch(IEnumerable? source)
    {
        _watched?.CollectionChanged -= OnGroupsChanged;
        _watched = source as INotifyCollectionChanged;
        _watched?.CollectionChanged += OnGroupsChanged;
    }

    private void OnGroupsChanged(object? sender, NotifyCollectionChangedEventArgs e) => Sync();

    // Keeps the block of every group that stays, drops the ones that went, builds the new, and puts them in the groups' order.
    private void Sync()
    {
        var groups = ItemsSource?.Cast<object>().Where(g => g is IRowGroup).ToList() ?? [];
        foreach (var gone in _blocks.Keys.Where(k => !groups.Contains(k, ReferenceEqualityComparer.Instance)).ToList())
        {
            _ = _stack.Children.Remove(_blocks[gone].Root);
            _ = _blocks.Remove(gone);
        }

        foreach (var group in groups.Where(g => !_blocks.ContainsKey(g)))
        {
            var block = new Block(group, ((IRowGroup)group).Items);
            block.Apply(HeaderTemplate, ItemTemplate);
            _blocks[group] = block;
        }

        _order.Clear();
        _order.AddRange(groups);
        for (var i = 0; i < groups.Count; i++)
        {
            var root = _blocks[groups[i]].Root;
            var at = _stack.Children.IndexOf(root);
            if (at < 0)
            {
                _stack.Children.Insert(i, root);
            }
            else if (at != i)
            {
                _stack.Children.Move(at, i);
            }
        }

        FollowScroll();
    }

    // The header that sticks is the last one whose block has reached the top; the next block's header pushes it out as it arrives.
    private void FollowScroll()
    {
        var offset = _scroll.Offset.Y;
        if (_order.Count == 0 || offset <= 0.5)
        {
            ShowSticky(group: null, push: 0);
            return;
        }

        var active = 0;
        for (var i = 0; i < _order.Count; i++)
        {
            if (_blocks[_order[i]].Root.Bounds.Top <= offset + 0.5)
            {
                active = i;
            }
        }

        var height = _blocks[_order[active]].Header.Bounds.Height;
        var next = active + 1 < _order.Count ? _blocks[_order[active + 1]].Root.Bounds.Top : double.PositiveInfinity;
        ShowSticky(_order[active], Math.Min(0, next - offset - height));
    }

    private void ShowSticky(object? group, double push)
    {
        if (!ReferenceEquals(_sticky.Content, group))
        {
            _sticky.Content = group;
        }

        _sticky.IsVisible = group is not null;
        if (Math.Abs(_push.Y - push) > 0.01)
        {
            _push.Y = push;
        }
    }

    // A group's header and its rows, kept as one control while the group stays.
    private sealed class Block
    {
        private readonly ItemsControl _items;

        internal Block(object group, IEnumerable items)
        {
            Header = new ContentControl { Content = group };
            _items = new ItemsControl { ItemsSource = items };
            Root = new StackPanel { Children = { Header, _items } };
        }

        internal StackPanel Root { get; }

        internal ContentControl Header { get; }

        internal void Apply(IDataTemplate? header, IDataTemplate? item)
        {
            Header.ContentTemplate = header;
            _items.ItemTemplate = item;
        }
    }
}
