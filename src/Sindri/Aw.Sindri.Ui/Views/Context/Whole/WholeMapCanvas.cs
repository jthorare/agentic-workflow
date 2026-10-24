using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Views.Context.Whole;

/// <summary>
/// The whole map's world (whole-map spec §3.2), drawn in one <see cref="Render"/> pass: every edge and node (or every tile) of the answer, and no control per
/// node. Below the dot zoom nodes are dots in their area's colour with one label per area; above it, chips with their words, only for what is on screen.
/// Lives in a <see cref="Controls.ZoomBorder"/>, which moves and scales it; the host tells it the zoom and the part of the world on screen.
/// </summary>
internal sealed partial class WholeMapCanvas : Control
{
    /// <summary>The map drawn.</summary>
    internal static readonly StyledProperty<WholeMapViewModel?> MapProperty = AvaloniaProperty.Register<WholeMapCanvas, WholeMapViewModel?>(nameof(Map));

    /// <summary>The zoom scale, which decides dots or chips.</summary>
    internal static readonly StyledProperty<double> ScaleProperty = AvaloniaProperty.Register<WholeMapCanvas, double>(nameof(Scale), 1);

    /// <summary>The part of the world on screen, so only that is drawn.</summary>
    internal static readonly StyledProperty<Rect> ViewportProperty = AvaloniaProperty.Register<WholeMapCanvas, Rect>(nameof(Viewport));

    static WholeMapCanvas()
    {
        AffectsRender<WholeMapCanvas>(MapProperty, ScaleProperty, ViewportProperty);
        AffectsMeasure<WholeMapCanvas>(MapProperty);
        AutomationProperties.AccessibilityViewProperty.OverrideDefaultValue<WholeMapCanvas>(AccessibilityView.Raw);   // the dots have no nodes of their own: the list of features is the accessible map
    }

    /// <summary>Gets or sets the map drawn.</summary>
    internal WholeMapViewModel? Map
    {
        get => GetValue(MapProperty);
        set => SetValue(MapProperty, value);
    }

    /// <summary>Gets or sets the zoom scale.</summary>
    internal double Scale
    {
        get => GetValue(ScaleProperty);
        set => SetValue(ScaleProperty, value);
    }

    /// <summary>Gets or sets the part of the world on screen.</summary>
    internal Rect Viewport
    {
        get => GetValue(ViewportProperty);
        set => SetValue(ViewportProperty, value);
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize) => Map?.Geometry is { } geometry ? new Size(geometry.World.Width, geometry.World.Height) : default;

    /// <inheritdoc/>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (Map is { } map && !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            var at = e.GetPosition(this);
            map.PointerMoved(at.X, at.Y, Slop);
        }
    }

    /// <inheritdoc/>
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        Map?.Hover(key: null);
    }

    /// <summary>The key under a point of the world, with the slop that keeps a dot easy to hit.</summary>
    /// <param name="at">The point.</param>
    /// <returns>The key; null over nothing.</returns>
    internal string? KeyAt(Point at) => Map?.KeyAt(at.X, at.Y, Slop);

    /// <summary>A click on the world, as the stage delivers it (a press here starts a pan, so the release never lands on this control).</summary>
    /// <param name="at">The point of the world.</param>
    internal void Click(Point at) => Map?.PointerClicked(at.X, at.Y, Slop);

    // A dot is 10 px on screen whatever the zoom, so what counts as on it grows as the world shrinks.
    private double Slop => WholeMapViewModel.UsesDots(Scale) ? 6 / Math.Max(Scale, 0.05) : 0;
}
