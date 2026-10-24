namespace Aw.UiDriver.Core.Protocol;

/// <summary>Scrolls a scroll viewer by an offset (the one holding the element, or the element itself when it is one).</summary>
/// <param name="Dx">Pixels to the right (negative: left).</param>
/// <param name="Dy">Pixels down (negative: up).</param>
/// <param name="Selector">The element; the first scroll viewer when null.</param>
internal sealed record ScrollRequest(double Dx, double Dy, Selector? Selector = null);
