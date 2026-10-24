namespace Aw.UiDriver.Core.Protocol;

/// <summary>Clicks an element as a person would: a button is pressed, a toggle flips, a list item is selected, an expander opens or closes.</summary>
/// <param name="Selector">The element.</param>
internal sealed record ClickRequest(Selector Selector);
