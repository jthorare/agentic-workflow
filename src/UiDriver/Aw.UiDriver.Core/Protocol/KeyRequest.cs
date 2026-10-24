namespace Aw.UiDriver.Core.Protocol;

/// <summary>Presses a key chord on an element (the focused one when no selector is given).</summary>
/// <param name="Chord">The chord: 'Enter', 'Escape', 'Ctrl+K', 'Ctrl+Shift+P'. The key is an Avalonia Key name.</param>
/// <param name="Selector">The element; the focused element when null.</param>
internal sealed record KeyRequest(string Chord, Selector? Selector = null);
