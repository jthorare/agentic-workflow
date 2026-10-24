namespace Aw.UiDriver.Core.Protocol;

/// <summary>What a selector and a wait look at on one element.</summary>
/// <param name="Id">The node id.</param>
/// <param name="Type">The type name.</param>
/// <param name="AutomationId">The automation id, when it has one.</param>
/// <param name="Name">The automation name or x:Name, when it has one.</param>
/// <param name="Text">The shown text, when it shows some.</param>
/// <param name="Visible">Whether it is visible (shown, on screen and not collapsed by an ancestor).</param>
/// <param name="Enabled">Whether it takes input.</param>
internal sealed record NodeFacts(string Id, string Type, string? AutomationId, string? Name, string? Text, bool Visible, bool Enabled);
