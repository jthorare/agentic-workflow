namespace Aw.UiDriver.Core.Protocol;

/// <summary>Puts an element in its pointer-over state, or takes it out of it, so a screenshot shows the hover look; nothing moves the real pointer.</summary>
/// <param name="Selector">The element.</param>
/// <param name="Off">True to take the state off again; false (the default) to set it.</param>
internal sealed record HoverRequest(Selector Selector, bool Off = false);
