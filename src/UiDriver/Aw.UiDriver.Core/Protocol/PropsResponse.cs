namespace Aw.UiDriver.Core.Protocol;

/// <summary>One element's properties.</summary>
/// <param name="Node">The element.</param>
/// <param name="Properties">Its registered properties by name, as text (a property that can't be read is left out).</param>
internal sealed record PropsResponse(NodeInfo Node, IReadOnlyDictionary<string, string> Properties);
