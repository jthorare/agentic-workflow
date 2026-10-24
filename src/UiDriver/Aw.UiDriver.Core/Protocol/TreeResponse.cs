namespace Aw.UiDriver.Core.Protocol;

/// <summary>The tree below an element.</summary>
/// <param name="Root">The element and its children to the depth asked for.</param>
internal sealed record TreeResponse(NodeInfo Root);
