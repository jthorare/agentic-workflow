namespace Aw.UiDriver.Core.Protocol;

/// <summary>Invokes a named action on an element.</summary>
/// <param name="Selector">The element.</param>
/// <param name="Action">'invoke' (a button's command or click), 'toggle', 'select', 'expand', 'collapse' or 'focus'.</param>
internal sealed record ActionRequest(Selector Selector, string Action);
