namespace Aw.UiDriver.Core.Protocol;

/// <summary>The window's size after the request.</summary>
/// <param name="Width">Width, in device-independent pixels.</param>
/// <param name="Height">Height, in device-independent pixels.</param>
/// <param name="Title">The window's title.</param>
internal sealed record WindowResponse(int Width, int Height, string Title);
