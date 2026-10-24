using Avalonia.Controls;

namespace Aw.Sindri.Ui.Views.Context.Whole;

/// <summary>The whole map's highlight (<see cref="WholeMapCanvas"/>, continued): what the pointer is over, or a click kept, stays at full strength and the rest dims, over Motion.Fast (instantly under reduced motion).</summary>
internal sealed partial class WholeMapCanvas
{
    private const double Dimmed = 0.42;   // the mockup's .dimmed

    private string? _focus;
    private IReadOnlySet<string>? _set;
    private (double From, double To, TimeSpan? Start, TimeSpan Length, int Id)? _fade;
    private int _fades;

    /// <summary>Gets how far the rest has dimmed, 0 (not at all) to 1 (fully).</summary>
    internal double DimStrength { get; private set; }

    /// <summary>Highlights a key and what is linked to it, or lets the highlight go.</summary>
    /// <param name="focus">The key the highlight is about; null for none.</param>
    /// <param name="keys">The key and its neighbours; null for none.</param>
    internal void Highlight(string? focus, IReadOnlySet<string>? keys)
    {
        if (keys is not null)
        {
            (_focus, _set) = (focus, keys);
        }

        var length = this.TryFindResource("Motion.Fast", ActualThemeVariant, out var value) && value is TimeSpan span ? span : TimeSpan.Zero;
        var to = keys is null ? 0.0 : 1.0;
        if (length <= TimeSpan.Zero || TopLevel.GetTopLevel(this) is not { } top || Math.Abs(to - DimStrength) < 0.001)
        {
            Settle(to);
            return;
        }

        var id = ++_fades;
        _fade = (DimStrength, to, null, length, id);
        top.RequestAnimationFrame(now => Step(now, id));
    }

    // One chain of frames per change: a callback from a change that was replaced finds another id and stops.
    private void Step(TimeSpan now, int id)
    {
        if (_fade is not { } fade || fade.Id != id)
        {
            return;
        }

        var start = fade.Start ?? now;
        var t = Math.Clamp((now - start) / fade.Length, 0, 1);
        if (t >= 1)
        {
            Settle(fade.To);
            return;
        }

        _fade = fade with { Start = start };
        DimStrength = fade.From + ((fade.To - fade.From) * t);
        InvalidateVisual();
        TopLevel.GetTopLevel(this)?.RequestAnimationFrame(next => Step(next, id));
    }

    private void Settle(double dim)
    {
        _fade = null;
        DimStrength = dim;
        if (dim == 0)
        {
            (_focus, _set) = (null, null);
        }

        InvalidateVisual();
    }

    // The opacity of a key: full when nothing is highlighted or it is the focus or linked to it, else fading toward the dimmed one.
    private double Strength(string key) => _set is null || _set.Contains(key) ? 1 : Faded(Dimmed);

    private double Faded(double to) => _set is null ? 1 : 1 - (DimStrength * (1 - to));

    // An edge touches the highlight when the focus is one of its ends; with no focus (a search), when both its ends are lit.
    private bool Touches(string from, string to) => _focus is { } focus
        ? string.Equals(from, focus, StringComparison.Ordinal) || string.Equals(to, focus, StringComparison.Ordinal)
        : _set is { } lit && lit.Contains(from) && lit.Contains(to);
}
