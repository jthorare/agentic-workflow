namespace Aw.Sindri.Ui.Views.Controls;

/// <summary>The scales a <see cref="ZoomBorder"/> allows: 62 % to 100 % for the context map; another stage sets its own.</summary>
/// <param name="Min">The smallest scale.</param>
/// <param name="Max">The largest scale.</param>
internal sealed record ZoomRange(double Min, double Max)
{
    /// <summary>Gets the context map's range: the 62 % floor keeps a node's words readable at 900 px, and the map is never drawn bigger than it was laid out.</summary>
    internal static ZoomRange Default { get; } = new(0.62, 1.0);

    /// <summary>The scale held inside the range.</summary>
    /// <param name="scale">A wanted scale.</param>
    /// <returns>The scale, no smaller than <see cref="Min"/> and no larger than <see cref="Max"/>.</returns>
    internal double Clamp(double scale) => Math.Clamp(scale, Min, Max);
}
