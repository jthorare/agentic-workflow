namespace Aw.UiDriver.Core.Protocol;

/// <summary>Types text into a text box (the focused one when no selector is given).</summary>
/// <param name="Text">The text.</param>
/// <param name="Selector">The text box; the focused element when null.</param>
/// <param name="Replace">True replaces what the box holds; false (the default) appends.</param>
internal sealed record TypeRequest(string Text, Selector? Selector = null, bool Replace = false);
