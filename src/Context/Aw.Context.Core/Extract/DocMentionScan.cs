using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Extract;

/// <summary>What <see cref="DocMentions.Find"/> found.</summary>
/// <param name="Edges">The edges, sorted.</param>
/// <param name="DocsWithMissing">How many docs cite a path that is not in the repo.</param>
/// <param name="MissingPaths">How many distinct such paths.</param>
public sealed record DocMentionScan(IReadOnlyList<GraphEdge> Edges, int DocsWithMissing, int MissingPaths);
