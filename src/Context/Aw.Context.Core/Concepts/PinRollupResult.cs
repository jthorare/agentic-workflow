using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Concepts;

/// <summary>The concept-level edges of a build and the notes the build report carries about them.</summary>
/// <param name="Edges">The pins' roll-up edges and declared edges, one edge per key.</param>
/// <param name="Notes">'declared, not observed' and 'observed, not declared' lines; never a refusal.</param>
public sealed record PinRollupResult(IReadOnlyList<GraphEdge> Edges, IReadOnlyList<string> Notes);
