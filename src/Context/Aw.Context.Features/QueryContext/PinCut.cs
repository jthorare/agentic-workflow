using Aw.Context.Core.Concepts;
using Aw.Context.Core.Graph;

namespace Aw.Context.Features.QueryContext;

/// <summary>The graph part under <c xml:lang="en">concept=</c> (concept pins spec §3.2): only the pin's nodes, and the stores they write.</summary>
internal static class PinCut
{
    /// <summary>Cuts a graph part to one pin.</summary>
    /// <param name="part">The graph part as the anchors built it.</param>
    /// <param name="scope">The pin.</param>
    /// <param name="graph">The graph the part was read from.</param>
    /// <returns>The part with only the pin's candidates, the anchors, other pins and the data stores the pin's nodes use.</returns>
    internal static GraphPart Apply(GraphPart part, PinScope scope, GraphSnapshot graph) => throw new NotSupportedException();
}
