using Aw.Context.Core.ConceptSynthesis;
using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Concepts;

/// <summary>Puts the synthesized concepts (the cross-source groups the model named) into a map graph as 'concept:&lt;key&gt;' nodes with their evidence and 'belongs-to' edges.</summary>
public static class SynthesizedConceptNodes
{
    /// <summary>Adds a node for each concept the model has labeled, or merges its evidence into the node already there, and joins the member records and the features they touch to it.</summary>
    /// <param name="graph">The graph so far, with the derived concepts.</param>
    /// <param name="concepts">Every stored synthesized concept; only the 'done' ones count.</param>
    /// <returns>The graph with the concepts, in the same order for the same input.</returns>
    public static GraphSnapshot Apply(GraphSnapshot graph, IReadOnlyList<SynthesizedConcept> concepts) =>
        throw new NotSupportedException();
}
