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

    private const double GlyphBox = 16;
    private const double SelBarWidth = 2;
    private const double RingStroke = 2;
    private const double RingRadius = 3;   // the centre line of the 2 px stroke: the outer edge has the 4 px corner of the grammar

    private IRowItem? _watched;
    private RowGlyph _glyph;
    private RowGlyphPainter.Palette? _palette;
    private IBrush? _selBar;
    private Pen? _ring;
    private double? _inset;
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
            _palette = null;
            _selBar = null;
            _ring = null;
            _inset = null;
            InvalidateVisual();
        };
    }

    /// <summary>How many cell controls rows ever made. A flat row has none, so it stays 0 (the recycle test still reads it until #3189 slice 4).</summary>
    internal static long CellsBuilt => 0;

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

        if (DataContext is IRowItem)
        {
            _palette ??= RowGlyphPainter.PaletteFor(this);
            _inset ??= Resource("Row.Inset", 24);
            using (context.PushTransform(Matrix.CreateTranslation(_inset.Value, (size.Height - GlyphBox) / 2)))
            {
                RowGlyphPainter.Paint(context, _glyph, _palette);
            }
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
        InvalidateVisual();
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

    private IBrush Brush(string key) => this.TryFindResource(key, ActualThemeVariant, out var v) && v is IBrush b ? b : Brushes.Gray;

    private double Resource(string key, double fallback) => this.TryFindResource(key, ActualThemeVariant, out var v) && v is double d ? d : fallback;
}
