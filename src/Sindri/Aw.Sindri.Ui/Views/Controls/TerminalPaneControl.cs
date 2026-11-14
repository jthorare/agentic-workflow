using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Aw.Sindri.Ui.Terminal;

namespace Aw.Sindri.Ui.Views.Controls;

/// <summary>
/// Draws a <see cref="TerminalGrid"/> (agents page spec §15.7, way C): a background rectangle per run of equal background, one
/// text run per run of equal colour and attributes, and the cursor as a filled block (focused) or a hollow one. It owns no
/// process and no socket: the page gives it frames and listens to <see cref="FitChanged"/>.
/// </summary>
#pragma warning disable MA0182 // Placed by the terminal view (U7a) when it lands.
internal sealed class TerminalPaneControl : Control, ITerminalPane
{
    /// <summary>The font size in px.</summary>
    internal static readonly StyledProperty<double> TerminalFontSizeProperty =
        AvaloniaProperty.Register<TerminalPaneControl, double>(nameof(TerminalFontSize), 13);

    /// <summary>True while the pane has focus: the cursor is a filled block, else a hollow one.</summary>
    internal static readonly StyledProperty<bool> HasPaneFocusProperty =
        AvaloniaProperty.Register<TerminalPaneControl, bool>(nameof(HasPaneFocus));

    /// <summary>True for a cached or frozen frame: drawn dimmed.</summary>
    internal static readonly StyledProperty<bool> DimmedProperty =
        AvaloniaProperty.Register<TerminalPaneControl, bool>(nameof(Dimmed));

    /// <summary>How many rows back from the live screen the view shows (scrollback).</summary>
    internal static readonly StyledProperty<int> ScrollOffsetProperty =
        AvaloniaProperty.Register<TerminalPaneControl, int>(nameof(ScrollOffset));

    private readonly TerminalBrushCache _brushes = new();
    private TerminalGrid? _grid;
    private (int Columns, int Rows) _fit;

    static TerminalPaneControl()
    {
        AffectsRender<TerminalPaneControl>(TerminalFontSizeProperty, HasPaneFocusProperty, DimmedProperty, ScrollOffsetProperty);
        _ = TerminalFontSizeProperty.Changed.AddClassHandler<TerminalPaneControl>((s, _) => s.Remeasure());
    }

    /// <summary>Initializes a new instance of the <see cref="TerminalPaneControl"/> class; its default name for assistive technology is "Terminal".</summary>
    public TerminalPaneControl() => AutomationProperties.SetName(this, "Terminal");

    /// <summary>Raised when the drawing area fits another number of columns or rows.</summary>
    public event EventHandler<TerminalSizeEventArgs>? FitChanged;

    /// <summary>Gets or sets the font size in px.</summary>
    internal double TerminalFontSize
    {
        get => GetValue(TerminalFontSizeProperty);
        set => SetValue(TerminalFontSizeProperty, value);
    }

    /// <summary>Gets or sets a value indicating whether the pane has focus.</summary>
    internal bool HasPaneFocus
    {
        get => GetValue(HasPaneFocusProperty);
        set => SetValue(HasPaneFocusProperty, value);
    }

    /// <summary>Gets or sets a value indicating whether the frame is drawn dimmed.</summary>
    internal bool Dimmed
    {
        get => GetValue(DimmedProperty);
        set => SetValue(DimmedProperty, value);
    }

    /// <summary>Gets or sets the rows shown back from the live screen.</summary>
    internal int ScrollOffset
    {
        get => GetValue(ScrollOffsetProperty);
        set => SetValue(ScrollOffsetProperty, value);
    }

    /// <summary>Gets the rows of scrollback above the live screen in the grid last drawn.</summary>
    internal int History => _grid?.History ?? 0;

    /// <summary>Gets the rows of the live screen: one page of scrolling.</summary>
    internal int PageRows => _grid?.Height ?? 0;

    /// <summary>Scrolls the pane's own scrollback, clamped to the history drawn: a positive number reads further back.</summary>
    /// <param name="rows">Rows to move: positive back, negative towards the live screen.</param>
    internal void ScrollBy(int rows) => ScrollOffset = Math.Clamp(ScrollOffset + rows, 0, History);

    /// <summary>Gets how many colour brushes this control has made (a test seam: painting the same frame again must make none).</summary>
    internal int BrushesCreated => _brushes.Created;

    /// <summary>Gets the size of one cell in px (0 before the first measure).</summary>
    internal Size CellSize { get; private set; }

    /// <inheritdoc/>
    public void Draw(TerminalGrid grid)
    {
        _grid = grid;
        InvalidateVisual();
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        if (_grid is null)
        {
            return;
        }

        EnsureCell();
        using var opacity = context.PushOpacity(Dimmed ? 0.5 : 1);
        var painter = new TerminalPainter(context, CellSize, Typeface(), TerminalFontSize, Brush("Brush.Ink"), Brush("Brush.Canvas"), _brushes);
        painter.Paint(_grid, ScrollOffset, HasPaneFocus);
    }

    /// <inheritdoc/>
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        ReportFit();
    }

    private void Remeasure()
    {
        CellSize = default;
        ReportFit();
    }

    private void ReportFit()
    {
        EnsureCell();
        if (CellSize.Width <= 0 || CellSize.Height <= 0)
        {
            return;
        }

        var fit = ((int)(Bounds.Width / CellSize.Width), (int)(Bounds.Height / CellSize.Height));
        if (fit != _fit && fit.Item1 > 0 && fit.Item2 > 0)
        {
            _fit = fit;
            FitChanged?.Invoke(this, new TerminalSizeEventArgs(fit.Item1, fit.Item2));
        }
    }

    private void EnsureCell()
    {
        if (CellSize.Width > 0)
        {
            return;
        }

        var text = new FormattedText("M", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface(), TerminalFontSize, Brushes.White);
        CellSize = new Size(text.Width, Math.Ceiling(text.Height));
    }

    private Typeface Typeface() =>
        this.TryFindResource("Font.Mono", ActualThemeVariant, out var found) && found is FontFamily family
            ? new Typeface(family)
            : new Typeface(FontFamily.Default);

    private IBrush Brush(string key) =>
        this.TryFindResource(key, ActualThemeVariant, out var value) && value is IBrush brush ? brush : Brushes.Gray;
}
