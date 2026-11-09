using Aw.Kernel;
using Aw.Sindri.Core.Graph.Validation;

namespace Aw.Sindri.Core.Graph.Ops;

/// <summary>Applies a batch of <see cref="GraphOp"/>s to a workflow graph.</summary>
public static class GraphOps
{
    /// <summary>
    /// Applies <paramref name="ops"/> in order to a copy of <paramref name="spec"/>: all of them or none. A <paramref name="baseRevision"/> that is not
    /// <paramref name="currentRevision"/> is SND-GRAPH-007 naming the current revision; an op that can't apply is SND-GRAPH-012 naming the op's number;
    /// a result the validator blocks (draft mode, so unwired nodes pass) is SND-GRAPH-002 listing the findings. <paramref name="spec"/> is never changed.
    /// </summary>
    /// <param name="spec">The graph at <paramref name="currentRevision"/>.</param>
    /// <param name="baseRevision">The revision the caller read before it made these ops.</param>
    /// <param name="currentRevision">The latest revision now.</param>
    /// <param name="ops">The edits, applied in order.</param>
    /// <param name="validator">Checks the edited graph.</param>
    /// <returns>The edited graph, or why nothing changed.</returns>
    public static Result<GraphSpec> Apply(GraphSpec spec, int baseRevision, int currentRevision, IReadOnlyList<GraphOp> ops, GraphValidator validator) =>
        throw new NotSupportedException("GraphOps.Apply is not written yet");
}
