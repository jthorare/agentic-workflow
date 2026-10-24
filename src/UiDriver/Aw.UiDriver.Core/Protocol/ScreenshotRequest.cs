namespace Aw.UiDriver.Core.Protocol;

/// <summary>A picture of the window, or of one element, as PNG.</summary>
/// <param name="Selector">The element; the window when null.</param>
internal sealed record ScreenshotRequest(Selector? Selector = null);
