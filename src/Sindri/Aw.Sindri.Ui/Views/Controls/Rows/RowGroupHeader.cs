using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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
    private const double FocusRing = 2;

    private readonly TextBlock _chevron = Caption(FontWeight.Normal);
    private readonly Border _chevronBox;
    private readonly TextBlock _heading = Caption(FontWeight.Medium);
    private readonly TextBlock _count = Caption(FontWeight.Normal);
    private readonly TextBlock _summary = Caption(FontWeight.Normal);
    private bool _ringOn;
    private INotifyPropertyChanged? _watchedGroup;
    private INotifyCollectionChanged? _watchedItems;

    public RowGroupHeader()
    {
        Classes.Add("group-header");
        FocusAdorner = null;   // no app-wide adorner: it spills 4 px into the neighbour rows and sits over the first row
        AddHandler(PointerPressedEvent, OnHeaderPressed, RoutingStrategies.Bubble, handledEventsToo: true);
        _ = SetValue(BackgroundProperty, Brushes.Transparent, Avalonia.Data.BindingPriority.Style);   // a border with no fill is not hit-testable: the padding left of the chevron would swallow the click. Style priority, so the sticky copy's template fill (Canvas) wins
        Focusable = true;   // the cursor can stand on a header: Left folds, Right unfolds
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
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        ArgumentNullException.ThrowIfNull(change);
        if (change.Property != IsKeyboardFocusWithinProperty)
        {
            return;
        }

        ApplyFocus();
    }

    /// <summary>True while the sticky copy stands in for a keyboard-focused header it covers: it draws the same ring (the copy never takes focus itself).</summary>
    internal bool MirrorsFocus
    {
        get;
        set
        {
            field = value;
            ApplyFocus();
        }
    }

    private void ApplyFocus()
    {
        var ring = IsKeyboardFocusWithin || MirrorsFocus;
        if (ring == _ringOn)
        {
            return;   // a scroll re-checks the mirror on every tick; the ring goes on and off once
        }

        _ringOn = ring;
        if (ring)
        {
            _ = Bind(BackgroundProperty, this.GetResourceObservable("Brush.Surface2"));
            var pad = Padding;   // the styled padding, read before the local one goes on
            Padding = new Thickness(pad.Left - FocusRing, pad.Top - FocusRing, pad.Right - FocusRing, pad.Bottom - FocusRing);   // the ring takes 2 px of the box: chevron and heading stay where they are
            BorderThickness = new Thickness(FocusRing);
            _ = Bind(BorderBrushProperty, this.GetResourceObservable("Brush.PrimaryHover"));   // the same brush the row's cursor ring uses; 1.18:1 fill alone is no cursor
        }
        else
        {
            ClearValue(BackgroundProperty);   // back to the fill under the local one: Transparent, or the sticky copy's template Canvas
            ClearValue(PaddingProperty);
            ClearValue(BorderThicknessProperty);
            ClearValue(BorderBrushProperty);
        }
    }

    private void OnHeaderPressed(object? sender, PointerPressedEventArgs e)
    {
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
        NameChevron(heading, collapsed);
        NameHeader(group, heading, collapsed);
        Classes.Set("collapsed", collapsed);
    }

    private void NameChevron(IRowGroupHeading? heading, bool collapsed) =>
        AutomationProperties.SetName(_chevronBox, heading is null ? string.Empty : (collapsed ? "Unfold " : "Fold ") + heading.Heading);

    // "Needs you, 8 rows, expanded": what the screen reader hears on the header (Home spec 7).
    private void NameHeader(IRowGroup? group, IRowGroupHeading? heading, bool collapsed)
    {
        AutomationProperties.SetControlTypeOverride(this, AutomationControlType.Group);
        AutomationProperties.SetName(this, group is null ? string.Empty : (heading?.Heading ?? group.ToString()) + ", " + RowSpeech.Rows(Count(group.Items)) + (heading is null ? string.Empty : collapsed ? ", collapsed" : ", expanded"));
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
