using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Line;

/// <summary>Owner answers as decision records on the context map (scoping spec C14, line spec LN-CTX1): owner-authored 'decision' records linked to the map's nodes, keyed by the packet's hash, so the next scoping's history lens sees them.</summary>
public interface IOwnerDecisions
{
    /// <summary>Stores the decisions of a run; the same packet hash and answers again store nothing twice.</summary>
    /// <param name="run">The run, which is the instance's address.</param>
    /// <param name="packet">The hash of the frozen packet the answers belong to.</param>
    /// <param name="decisions">The decisions; none stores nothing.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>How many were stored, or why none were (the ingest does not exist yet); an error when the call failed.</returns>
    public ValueTask<Result<OwnerDecisionsIngested>> Ingest(InstanceAddress run, string packet, IReadOnlyList<OwnerDecision> decisions, CancellationToken cancellationToken);
}
