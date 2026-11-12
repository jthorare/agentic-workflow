using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Rows;

namespace Aw.Sindri.Ui.Views.Controls.Rows;

/// <summary>
/// A group header of a <see cref="RowList"/> (#2978): chevron, sentence-case heading, count, optional right-hand summary. 28 px, class group-header. A group that is an
/// <see cref="IRowGroupHeading"/> folds: a click anywhere on the header (or its 28 px chevron) flips <see cref="IRowGroupHeading.IsCollapsed"/>, and the header shows the
/// collapsed state with class collapsed. The header follows its group: heading, summary, fold state and row count update in place.
/// </summary>
internal sealed class RowGroupHeader : Border
{
    private const double ChevronBox = 28;

    private readonly TextBlock _chevron = Caption(FontWeight.Normal);
    private readonly Border _chevronBox;
    private readonly TextBlock _heading = Caption(FontWeight.Medium);
    private readonly TextBlock _count = Caption(FontWeight.Normal);
    private readonly TextBlock _summary = Caption(FontWeight.Normal);
    private INotifyPropertyChanged? _watchedGroup;
    private INotifyCollectionChanged? _watchedItems;

    public RowGroupHeader()
    {
        Classes.Add("group-header");
        _chevron.HorizontalAlignment = HorizontalAlignment.Center;
        _chevronBox = new Border { Name = "PART_Chevron", Width = ChevronBox, Height = ChevronBox, Background = Brushes.Transparent, Cursor = new Cursor(StandardCursorType.Hand), Child = _chevron };
        _count.Margin = new Thickness(6, 0, 0, 0);
        _summary.HorizontalAlignment = HorizontalAlignment.Right;
        _summary.TextAlignment = TextAlignment.Right;
        var left = new StackPanel { Orientation = Orientation.Horizontal, Children = { _chevronBox, _heading, _count } };
        Child = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { left } };
        Grid.SetColumn(_summary, 1);
        ((Grid)Child).Children.Add(_summary);
    }

    /// <summary>The group this header shows; null while the header is recycled.</summary>
    internal IRowGroup? Group { get; private set; }

    /// <summary>Shows a group.</summary>
    /// <param name="group">The group.</param>
    internal void Fill(IRowGroup? group)
    {
        Unwatch();
        Group = group;
        if (TopLevel.GetTopLevel(this) is not null)
        {
            Watch();
        }

        Update();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Watch();
        Update();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Unwatch();
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        ArgumentNullException.ThrowIfNull(e);
        if (Group is IRowGroupHeading heading && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            heading.IsCollapsed = !heading.IsCollapsed;
            e.Handled = true;
        }
    }

    private void Watch()
    {
        if (_watchedGroup is not null || _watchedItems is not null || Group is null)
        {
            return;
        }

        _watchedGroup = Group as INotifyPropertyChanged;
        _watchedItems = Group.Items as INotifyCollectionChanged;
        _watchedGroup?.PropertyChanged += OnGroupChanged;
        _watchedItems?.CollectionChanged += OnItemsChanged;
    }

    private void Unwatch()
    {
        _watchedGroup?.PropertyChanged -= OnGroupChanged;
        _watchedItems?.CollectionChanged -= OnItemsChanged;
        _watchedGroup = null;
        _watchedItems = null;
    }

    private void OnGroupChanged(object? sender, PropertyChangedEventArgs e) => Update();

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => Update();

    private void Update()
    {
        var group = Group;
        var heading = group as IRowGroupHeading;
        var collapsed = heading?.IsCollapsed == true;
        _heading.Text = heading?.Heading ?? group?.ToString() ?? string.Empty;
        _summary.Text = heading?.Summary;
        _count.Text = group is null ? string.Empty : Count(group.Items).ToString(CultureInfo.InvariantCulture);
        _chevron.Text = collapsed ? "▸" : "▾";
        _chevronBox.IsVisible = heading is not null;
        Classes.Set("collapsed", collapsed);
    }

    private static int Count(IEnumerable items) => items is ICollection collection ? collection.Count : items.Cast<object>().Count();

    private static TextBlock Caption(FontWeight weight)
    {
        var block = new TextBlock { FontWeight = weight, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.NoWrap };
        _ = block.Bind(TextBlock.ForegroundProperty, block.GetResourceObservable("Brush.InkSubtle"));
        _ = block.Bind(TextBlock.FontSizeProperty, block.GetResourceObservable("Type.Caption.Size"));
        return block;
    }
}
