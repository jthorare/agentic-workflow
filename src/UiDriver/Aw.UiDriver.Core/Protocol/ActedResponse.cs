namespace Aw.UiDriver.Core.Protocol;

/// <summary>A verb that acted on an element and has nothing more to say.</summary>
/// <param name="Node">The element it acted on.</param>
internal sealed record ActedResponse(NodeInfo Node);
