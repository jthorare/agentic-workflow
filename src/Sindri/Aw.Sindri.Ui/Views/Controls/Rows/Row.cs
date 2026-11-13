using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Aw.Sindri.Ui.ViewModels.Rows;

namespace Aw.Sindri.Ui.Views.Controls.Rows;

/// <summary>
/// One row of a <see cref="RowList"/> (#2978, #3189): a flat control. It has no template and no child controls; it draws itself in <see cref="Render"/> from its item (the
/// <see cref="Background"/> fill, the state glyph, the selection bar and the cursor ring). Its data context is an IRowItem. Classes: cursor (keyboard focus within),
/// sel (the row being peeked or open). Its automation name is the spoken sentence of the item; the class aw-drawn-text tells aw-ui-driver to report that name as the row's text.
/// </summary>
internal sealed partial class Row : Control
{
    /// <summary>The fill behind the row. The styles set it: Transparent at rest, so the whole row is hit-testable, Surface2 under the pointer, PrimarySoft when selected.</summary>
    public static readonly StyledProperty<IBrush?> BackgroundProperty = Border.BackgroundProperty.AddOwner<Row>();

    /// <summary>The fact columns of the list, in order.</summary>
    public static readonly StyledProperty<IReadOnlyList<RowColumn>?> ColumnsProperty = AvaloniaProperty.Register<Row, IReadOnlyList<RowColumn>?>(nameof(Columns));

    /// <summary>How many of the fact columns show; the list lowers it while the peek is open.</summary>
    public static readonly StyledProperty<int> VisibleColumnsProperty = AvaloniaProperty.Register<Row, int>(nameof(VisibleColumns), defaultValue: int.MaxValue);

    private static long _textLayoutsBuilt;

    private const double GlyphBox = 16;
    private const double SelBarWidth = 2;
    internal const double RingStroke = 2;
    internal const double RingCorner = 4;   // the outer corner of the ring, as the grammar says
    private const double RingRadius = RingCorner - (RingStroke / 2);   // the centre line of the stroke

    private IRowItem? _watched;
    private RowGlyph _glyph;
    private RowGlyphPainter.Palette? _palette;
    private IBrush? _selBar;
    private Pen? _ring;
    private Avalonia.Styling.ThemeVariant? _variant;

    static Row()
    {
        FocusableProperty.OverrideDefaultValue<Row>(defaultValue: true);
        AffectsRender<Row>(BackgroundProperty, ColumnsProperty, VisibleColumnsProperty);
    }

    public Row()
    {
        Classes.Add("aw-drawn-text");
        Classes.CollectionChanged += (_, _) => InvalidateVisual();   // cursor, sel and the pointer-over pseudo class change what is drawn
        ActualThemeVariantChanged += (_, _) =>
        {
            if (TopLevel.GetTopLevel(this) is null || Equals(_variant, ActualThemeVariant))
            {
                return;   // detached, or a recycled row re-entering the tree under the theme it already drew with
            }

            _variant = ActualThemeVariant;
            DropLayouts();
        };
        ResourcesChanged += (_, _) =>
        {
            // Avalonia raises ResourcesChanged on every re-attach too, and a recycled row is re-attached on every fill: only a moved text size drops the layouts.
            // Known limit (#3189, accepted in review): a brush swapped in the resource dictionary outside a theme flip does not drop the cached inks and chip brushes here, so the row
            // draws it on its next invalidation (a new item, a theme flip, a scaling change) and not at once. No feature swaps a brush that way today.
            if (TopLevel.GetTopLevel(this) is not null && TextSizeMoved())
            {
                DropLayouts();
            }
        };
    }

    /// <summary>How many text layouts rows ever built. A row builds its layouts once per item and keeps them, so this follows the fills and not the renders.</summary>
    internal static long TextLayoutsBuilt => Interlocked.Read(ref _textLayoutsBuilt);

    /// <summary>The fill behind the row.</summary>
    public IBrush? Background
    {
        get => GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
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
    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var size = Bounds.Size;
        if (Background is { } fill)
        {
            context.FillRectangle(fill, new Rect(size));   // a Transparent fill is drawn too: it is what makes the whole row hit-testable
        }

        if (DataContext is IRowItem item)
        {
            Prepare(item, size);   // first: it may drop the palette and the brushes after a theme flip
            _palette ??= RowGlyphPainter.PaletteFor(this);
            using (context.PushTransform(Matrix.CreateTranslation(MetricsNow().Inset, (size.Height - GlyphBox) / 2)))
            {
                RowGlyphPainter.Paint(context, _glyph, _palette);
            }

            DrawContent(context, item, size.Height);
        }

        if (Classes.Contains("sel"))
        {
            context.FillRectangle(_selBar ??= Brush("Brush.Primary"), new Rect(0, 0, SelBarWidth, size.Height));
        }

        if (Classes.Contains("cursor"))
        {
            _ring ??= new Pen(Brush("Brush.PrimaryHover"), RingStroke);
            context.DrawRectangle(brush: null, _ring, new Rect(RingStroke / 2, RingStroke / 2, size.Width - RingStroke, size.Height - RingStroke), RingRadius, RingRadius);
        }
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
            UpdateStrip();
        }
        else if (change.Property == ColumnsProperty || change.Property == VisibleColumnsProperty)
        {
            DropLayouts();
        }
        else if (change.Property == IsKeyboardFocusWithinProperty)
        {
            Classes.Set("cursor", IsKeyboardFocusWithin);
            UpdateStrip();
        }
        else if (change.Property == IsPointerOverProperty)
        {
            UpdateStrip();
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Watch(DataContext as IRowItem);
        ScalingSource = TopLevel.GetTopLevel(this);
        ScalingSource?.ScalingChanged += OnScalingChanged;
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Watch(item: null);
        ScalingSource?.ScalingChanged -= OnScalingChanged;
        ScalingSource = null;
        DropStrip();
    }

    // The row follows its item: a changed title, age or fact redraws it and renames it in place (IRowItem's doc).
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
        if (DataContext is not IRowItem item)
        {
            return;
        }

        _glyph = RowGlyphs.For(item.Kind, item.StateClass, item.StateKey);
        AutomationProperties.SetName(this, SpokenName(item, StateWord(item)));
        DropText();
        UpdateStrip();
    }

    // "<state>, <title>, <facts>, waits on <who>, <age>": what a screen reader says for the row, and what aw-ui-driver reports as its text.
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

    private void OnScalingChanged(object? sender, EventArgs e) => DropLayouts();   // the render scaling moved: the layouts are cut again at the new scale

    private IBrush Brush(string key) => this.TryFindResource(key, ActualThemeVariant, out var v) && v is IBrush b ? b : throw new InvalidOperationException($"The theme has no brush {key}; the row draws only from the tokens.");

    private double Resource(string key, double fallback) => this.TryFindResource(key, ActualThemeVariant, out var v) && v is double d ? d : fallback;
}
