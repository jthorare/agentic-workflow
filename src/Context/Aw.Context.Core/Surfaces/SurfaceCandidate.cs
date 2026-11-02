namespace Aw.Context.Core.Surfaces;

/// <summary>One thing the build proposes as a product surface, thing, action or flow, before Jev decides whether lookalikes are one node (model routing spec §6.9).</summary>
/// <param name="NodeKey">The node key the candidate would have, for example 'action:inclusions.grant'.</param>
/// <param name="Kind">The node kind: surface, thing, action or flow.</param>
/// <param name="Name">The proposed name; a merge takes one candidate's name exactly.</param>
/// <param name="Pass">Which pass of the build made it.</param>
/// <param name="Evidence">The file paths that show it.</param>
/// <param name="Edges">Its edges as text, for example 'on surface:billing' or 'writes thing:account'.</param>
public sealed record SurfaceCandidate(string NodeKey, string Kind, string Name, string Pass, IReadOnlyList<string> Evidence, IReadOnlyList<string> Edges);
