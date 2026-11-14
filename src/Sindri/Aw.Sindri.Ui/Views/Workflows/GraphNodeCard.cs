using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Aw.Sindri.Ui.ViewModels.Workflows;
using Aw.Sindri.Ui.Views.Controls;

namespace Aw.Sindri.Ui.Views.Workflows;

/// <summary>
/// One workflow step as a card (#3234): the title, the block type, and the run's status as a <see cref="StateChipView"/> (glyph, word, tone), 200 by 64. Its look in every state
/// comes from <see cref="GraphTilePens"/>: hover, a focus ring outside the selected ring (keyboard focus only), disabled (dashed), error. A screen reader reads "title, type, status".
/// </summary>
[SuppressMessage("Design", "MA0182", Justification = "The editor canvas adopts it in its own PR (#3234).")]
internal sealed class GraphNodeCard : UserControl
{
    private const double Radius = 8;

    /// <summary>The step's title.</summary>
    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<GraphNodeCard, string?>(nameof(Title));

    /// <summary>The block type, such as "run_agent".</summary>
    public static readonly StyledProperty<string?> KindProperty = AvaloniaProperty.Register<GraphNodeCard, string?>(nameof(Kind));

    /// <summary>The status word of the run shown; empty hides the chip.</summary>
    public static readonly StyledProperty<string?> StatusWordProperty = AvaloniaProperty.Register<GraphNodeCard, string?>(nameof(StatusWord));

    /// <summary>The status tone: ok, running, waiting, attention, error, neutral or draft.</summary>
    public static readonly StyledProperty<string?> StatusToneProperty = AvaloniaProperty.Register<GraphNodeCard, string?>(nameof(StatusTone));

    /// <summary>The glyph before the status word.</summary>
    public static readonly StyledProperty<string?> StatusGlyphProperty = AvaloniaProperty.Register<GraphNodeCard, string?>(nameof(StatusGlyph));

    /// <summary>Whether this is the selected step (the inner ring).</summary>
    public static readonly StyledProperty<bool> IsSelectedProperty = AvaloniaProperty.Register<GraphNodeCard, bool>(nameof(IsSelected));

    /// <summary>Whether the step failed or is invalid (the error fill and border).</summary>
    public static readonly StyledProperty<bool> IsInvalidProperty = AvaloniaProperty.Register<GraphNodeCard, bool>(nameof(IsInvalid));

    // A Canvas, not a Panel with negative margins: a ring sits outside the card at an explicit offset and size, so no layout rule can move it.
    private readonly Canvas _rings = new() { IsHitTestVisible = false, ClipToBounds = false };
    private readonly Border _face = new() { Name = "Card", CornerRadius = new CornerRadius(Radius), Padding = new Thickness(12, 8) };
    private readonly Rectangle _dash = new() { Name = "Dash", RadiusX = Radius, RadiusY = Radius, StrokeThickness = 1, StrokeDashArray = [6, 4], IsVisible = false, IsHitTestVisible = false };
    private readonly TextBlock _title = new() { TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _kind = new() { TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly StateChipView _chip = new() { Margin = new Thickness(8, 0, 0, 0) };
    private bool _hover;
    private bool _focusVisible;

    static GraphNodeCard()
    {
        _ = TitleProperty.Changed.AddClassHandler<GraphNodeCard>((c, _) => c.Refresh());
        _ = KindProperty.Changed.AddClassHandler<GraphNodeCard>((c, _) => c.Refresh());
        _ = StatusWordProperty.Changed.AddClassHandler<GraphNodeCard>((c, _) => c.Refresh());
        _ = StatusToneProperty.Changed.AddClassHandler<GraphNodeCard>((c, _) => c.Refresh());
        _ = StatusGlyphProperty.Changed.AddClassHandler<GraphNodeCard>((c, _) => c.Refresh());
        _ = IsSelectedProperty.Changed.AddClassHandler<GraphNodeCard>((c, _) => c.Paint());
        _ = IsInvalidProperty.Changed.AddClassHandler<GraphNodeCard>((c, _) => c.Paint());
        _ = IsEnabledProperty.Changed.AddClassHandler<GraphNodeCard>((c, _) => c.Paint());
    }

    /// <summary>Initializes a new instance of the <see cref="GraphNodeCard"/> class, 200 by 64.</summary>
    public GraphNodeCard()
    {
        Width = WorkflowGraphViewModel.CardWidth;
        Height = WorkflowGraphViewModel.CardHeight;
        Focusable = true;
        // The rings lie outside the card: nothing from here down to the rings may clip to the card's bounds (a clipped ring showed only the specks where its corners dip inside), and the
        // app-wide focus adorner (FluentOverrides.axaml, 2 to 4 px out) is off because the card draws its own focus ring.
        ClipToBounds = false;
        FocusAdorner = null;
        _title.Classes.Add("body-strong");
        _kind.Classes.Add("caption");
        AutomationProperties.SetAccessibilityView(_title, AccessibilityView.Raw);
        AutomationProperties.SetAccessibilityView(_kind, AccessibilityView.Raw);
        AutomationProperties.SetAccessibilityView(_chip, AccessibilityView.Raw);

        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), RowDefinitions = new RowDefinitions("Auto,Auto") };
        body.Children.Add(_title);
        Grid.SetColumn(_chip, 1);
        body.Children.Add(_chip);
        Grid.SetRow(_kind, 1);
        Grid.SetColumnSpan(_kind, 2);
        body.Children.Add(_kind);
        _face.Child = body;

        var layers = new Grid { ClipToBounds = false };
        layers.Children.Add(_rings);
        layers.Children.Add(_face);
        layers.Children.Add(_dash);
        Content = layers;
        Refresh();
    }

    /// <summary>Gets or sets the title.</summary>
    internal string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Gets or sets the block type.</summary>
    internal string? Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    /// <summary>Gets or sets the status word.</summary>
    internal string? StatusWord
    {
        get => GetValue(StatusWordProperty);
        set => SetValue(StatusWordProperty, value);
    }

    /// <summary>Gets or sets the status tone.</summary>
    internal string? StatusTone
    {
        get => GetValue(StatusToneProperty);
        set => SetValue(StatusToneProperty, value);
    }

    /// <summary>Gets or sets the status glyph.</summary>
    internal string? StatusGlyph
    {
        get => GetValue(StatusGlyphProperty);
        set => SetValue(StatusGlyphProperty, value);
    }

    /// <summary>Gets or sets a value indicating whether this is the selected step.</summary>
    internal bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    /// <summary>Gets or sets a value indicating whether the step failed or is invalid.</summary>
    internal bool IsInvalid
    {
        get => GetValue(IsInvalidProperty);
        set => SetValue(IsInvalidProperty, value);
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (e?.NameScope.Find<ContentPresenter>("PART_ContentPresenter") is { } presenter)
        {
            presenter.ClipToBounds = false;
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Paint();
    }

    /// <inheritdoc />
    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _hover = true;
        Paint();
    }

    /// <inheritdoc />
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hover = false;
        Paint();
    }

    /// <inheritdoc />
    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusVisible = e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional;
        Paint();
    }

    /// <inheritdoc />
    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusVisible = false;
        Paint();
    }

    // The words: the title, the type, the chip, and the one name a screen reader reads.
    private void Refresh()
    {
        _title.Text = Title;
        _kind.Text = Kind;
        _chip.Word = StatusWord;
        _chip.Tone = StatusTone;
        _chip.Glyph = StatusGlyph;
        _chip.IsVisible = !string.IsNullOrEmpty(StatusWord);
        AutomationProperties.SetName(this, string.Join(", ", new[] { Title, Kind, StatusWord }.Where(part => !string.IsNullOrEmpty(part))));
    }

    // The look: the pens' brush keys for the states it is in, resolved from the theme.
    private void Paint()
    {
        var states = CurrentStates();
        var paint = GraphTilePens.Paint(states);
        _face.Background = Resolve(paint.Fill);
        _face.BorderThickness = new Thickness(paint.BorderThickness);
        _face.BorderBrush = paint.Dashed ? Brushes.Transparent : Resolve(paint.Border);
        _dash.IsVisible = paint.Dashed;
        _dash.Stroke = Resolve(paint.Border);
        if (paint.ChipFill is { } chipFill && paint.ChipBorder is { } chipBorder && paint.ChipInk is { } chipInk)
        {
            _chip.Dim(Resolve(chipFill), Resolve(chipBorder), Resolve(chipInk));
        }
        else
        {
            _chip.ClearDim();
        }

        _title.Foreground = Resolve(paint.Title);
        _kind.Foreground = Resolve(paint.Kind);
        PaintRings(states);
    }

    private GraphTileStates CurrentStates()
    {
        var states = GraphTileStates.None;
        if (!IsEnabled)
        {
            states |= GraphTileStates.Disabled;
        }

        if (IsInvalid)
        {
            states |= GraphTileStates.Error;
        }

        if (_hover)
        {
            states |= GraphTileStates.Hover;
        }

        if (IsSelected)
        {
            states |= GraphTileStates.Selected;
        }

        if (_focusVisible)
        {
            states |= GraphTileStates.Focus;
        }

        return states;
    }

    private void PaintRings(GraphTileStates states)
    {
        _rings.Children.Clear();
        foreach (var ring in GraphTilePens.Rings(states))
        {
            var reach = ring.Gap + ring.Thickness;
            var band = new Border
            {
                Name = "Ring",
                BorderBrush = Resolve(ring.Key),
                BorderThickness = new Thickness(ring.Thickness),
                CornerRadius = new CornerRadius(Radius + reach),
                Width = WorkflowGraphViewModel.CardWidth + (2 * reach),
                Height = WorkflowGraphViewModel.CardHeight + (2 * reach),
                IsHitTestVisible = false,
            };
            Canvas.SetLeft(band, -reach);
            Canvas.SetTop(band, -reach);
            _rings.Children.Add(band);
        }
    }

    private IBrush Resolve(string key) => this.TryFindResource(key, ActualThemeVariant, out var value) && value is IBrush brush ? brush : Brushes.Gray;
}
