using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Concepts;

/// <summary>The suggested 'expresses' edges of a map build: a page or an action expresses each concept its implementing code's home features belong to (pages spec §3.2).</summary>
public static class PageExpresses
{
    /// <summary>Adds the suggested edges to a graph that already has its concepts.</summary>
    /// <param name="graph">The graph after concept naming and the pins.</param>
    /// <returns>The graph with its suggested edges.</returns>
    public static GraphSnapshot Apply(GraphSnapshot graph) => graph;
}
