using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Map;

/// <summary>The owner's named pages laid over the map at read time (pages spec §3.3): nothing is stored, so an approved edit shows on the next read and a rebuild loses nothing.</summary>
public static class PageOverlay
{
    /// <summary>Lays the profile's pages over a graph: relabels their surfaces and actions (or adds owner nodes) and links each action to its concepts, every touched node and edge carrying 'page:&lt;repo&gt;/&lt;page-id&gt;' evidence. Never removes a node.</summary>
    /// <param name="graph">The graph after the deny cut.</param>
    /// <param name="repos">The profile's repos, each with its pages.</param>
    /// <returns>The graph with the pages; the same graph when there are none. Applying it twice gives the same graph.</returns>
    public static GraphSnapshot Apply(GraphSnapshot graph, IReadOnlyList<ContextRepo> repos) => throw new NotSupportedException();

    /// <summary>The graph without any node or edge that carries a 'page:' evidence ref: what an export writes and an import keeps.</summary>
    /// <param name="graph">The graph.</param>
    /// <returns>The graph without the overlay.</returns>
    public static GraphSnapshot Without(GraphSnapshot graph) => throw new NotSupportedException();
}
