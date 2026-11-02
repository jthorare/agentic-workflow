using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Pages;

/// <summary>The action of the matched page a pasted line names.</summary>
/// <param name="Key">The action's key.</param>
/// <param name="Name">Its name on the map.</param>
public sealed record PageListActionMatch(NodeKey Key, string Name);
