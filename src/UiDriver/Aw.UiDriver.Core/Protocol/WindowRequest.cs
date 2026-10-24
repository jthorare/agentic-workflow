namespace Aw.UiDriver.Core.Protocol;

/// <summary>Reads the window's size, or sets it first.</summary>
/// <param name="Width">The new width, or null to leave it.</param>
/// <param name="Height">The new height, or null to leave it.</param>
internal sealed record WindowRequest(int? Width = null, int? Height = null);
