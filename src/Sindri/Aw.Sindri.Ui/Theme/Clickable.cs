using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Aw.Sindri.Ui.Theme;

#pragma warning disable MA0182 // why: ClickableStyles.axaml sets Clickable.Track from a style; the analyzer cannot see XAML
/// <summary>
/// The shared look of a clickable surface (#2535): a Border with the clickable class (Theme/ClickableStyles.axaml) lifts its
/// background and border under the pointer and takes a pressed look (the pressed fill and border; nothing moves, #3190) while a button is down, eased over Motion.Hover (zero under
/// reduced motion). The style sets <see cref="TrackProperty"/>, which gives the control its colour Transitions and keeps the
/// :pressed pseudo-class (a Border has none of its own) in step with the pointer.
/// </summary>
internal static class Clickable
{
    /// <summary>Set by the clickable style; true wires the control.</summary>
    public static readonly AttachedProperty<bool> TrackProperty = AvaloniaProperty.RegisterAttached<Border, Border, bool>("Track");

    static Clickable()
    {
        _ = TrackProperty.Changed.AddClassHandler<Border>(static (border, e) =>
        {
            if (e.NewValue is true)
            {
                Wire(border);
            }
        });
    }

    /// <summary>Gets <see cref="TrackProperty"/>.</summary>
    /// <param name="border">The border.</param>
    /// <returns>Whether it is wired.</returns>
    public static bool GetTrack(Border border) => border.GetValue(TrackProperty);

    /// <summary>Sets <see cref="TrackProperty"/>.</summary>
    /// <param name="border">The border.</param>
    /// <param name="value">Whether to wire it.</param>
    public static void SetTrack(Border border, bool value) => border.SetValue(TrackProperty, value);

    private static BrushTransition Colour(Border border, AvaloniaProperty<Avalonia.Media.IBrush?> property) =>
        new() { Property = property, Duration = Length(border), Easing = new SplineEasing(x1: 0.2, y1: 0, x2: 0, y2: 1) };

    private static void Wire(Border border)
    {
        border.Transitions = [Colour(border, Border.BackgroundProperty), Colour(border, Border.BorderBrushProperty)];
        // Reduced motion rewrites Motion.Hover; read it again as the pointer arrives so the next ease uses the current length.
        border.PointerEntered += (_, _) => Refresh(border);
        border.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(border).Properties.IsLeftButtonPressed)
            {
                Refresh(border);
                Set(border, pressed: true);
            }
        };
        border.PointerReleased += (_, _) => Set(border, pressed: false);
        border.PointerCaptureLost += (_, _) => Set(border, pressed: false);
        // A press holds the pointer (implicit capture), as a Button's does: a press can end with the pointer outside (it drags off the edge); the press lasts until the release or a lost capture, not until the pointer leaves. The capture
        // goes to what was hit, which is often a child (a TextBlock, a chip), so a holder inside the border counts as the border.
        border.PointerExited += (_, e) =>
        {
            if (!Holds(border, e.Pointer.Captured))
            {
                Set(border, pressed: false);
            }
        };
    }

    private static void Refresh(Border border)
    {
        if (border.Transitions is not { } transitions)
        {
            return;
        }

        var length = Length(border);
        foreach (var transition in transitions.OfType<BrushTransition>())
        {
            transition.Duration = length;
        }
    }

    private static bool Holds(Border border, IInputElement? captured) =>
        captured is Visual holder && (ReferenceEquals(holder, border) || border.IsVisualAncestorOf(holder));

    private static void Set(Border border, bool pressed) => ((IPseudoClasses)border.Classes).Set(":pressed", pressed);

    private static TimeSpan Length(Border border) =>
        Find(border) is TimeSpan span ? span : TimeSpan.FromMilliseconds(150);

    private static object? Find(Border border) =>
        border.TryFindResource("Motion.Hover", border.ActualThemeVariant, out var own) ? own
        : Application.Current is { } app && app.TryFindResource("Motion.Hover", out var shared) ? shared
        : null;
}
