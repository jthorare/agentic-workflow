using Aw.Context.Core.Graph;
using Aw.Context.Core.Search;

namespace Aw.Context.Features.SearchMap;

/// <summary>A node as the index knows it: the document it was indexed as, the repo its evidence points at and its trust.</summary>
/// <param name="Node">The node.</param>
/// <param name="Doc">The indexed document (cleaned, not fenced).</param>
/// <param name="Repo">The repo its evidence points at, or null.</param>
/// <param name="Trust">Whether its text may be read as instructions.</param>
internal sealed record IndexedNode(GraphNode Node, MapSearchDoc Doc, string? Repo, Core.Results.Trust Trust);
