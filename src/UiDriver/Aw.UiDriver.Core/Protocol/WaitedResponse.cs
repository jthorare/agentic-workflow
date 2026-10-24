namespace Aw.UiDriver.Core.Protocol;

/// <summary>A wait that held.</summary>
/// <param name="Node">The element, or null when the wait was for it to be gone.</param>
/// <param name="WaitedMs">How long it took.</param>
internal sealed record WaitedResponse(NodeInfo? Node, int WaitedMs);
