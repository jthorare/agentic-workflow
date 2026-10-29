using Aw.Context.Core.ConceptSynthesis;
using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Concepts;

/// <summary>
/// Drops the derived concepts a synthesized one covers (#1281): a derived concept goes, with every edge on it, when one synthesized concept holds at least the cover share of its features.
/// Its features keep their synthesized membership and the rest fall to 'other' on read. Pins, the catch-all and a derived concept that shares a synthesized key are never dropped.
/// </summary>
public static class CoveredConcepts
{
    /// <summary>Drops the covered derived concepts.</summary>
    /// <param name="graph">The graph with the derived and the synthesized concepts.</param>
    /// <param name="concepts">Every stored synthesized concept; only the 'done' ones cover.</param>
    /// <param name="share">The share of a derived concept's features one synthesized concept must hold (0.5 to 1).</param>
    /// <returns>The graph without the covered concepts, in the same order otherwise.</returns>
    public static CoverFold Fold(GraphSnapshot graph, IReadOnlyList<SynthesizedConcept> concepts, double share) => throw new NotSupportedException();
}
