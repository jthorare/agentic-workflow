using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>
/// The 'agent.op' payload (agents spec §5, §15.8): one claim per act id, never the body. A7 writes op 'start' after a 2xx start; the steer ops of the Agents page write the same event.
/// </summary>
/// <param name="RowId">The row the op applied to; for a start, 'sindri-&lt;actId&gt;'.</param>
/// <param name="Op">'start' for a one-off start.</param>
/// <param name="BodyHash">The sha256 of the canonical request body, lowercase hex; a replay with another body is SND-AGENT-009.</param>
/// <param name="Outcome">The stored 2xx answer: for a start, the pane id the answer carried ('%N'); the rest of the answer follows from the act id. A replay returns it.</param>
/// <param name="Worktree">For a start: the worktree folder it made, or null when it made none. The DELETE worktree route reads it.</param>
public sealed record AgentOpRecorded(AgentRowId RowId, string Op, string BodyHash, string Outcome, string? Worktree = null)
{
    /// <summary>The idempotency key of the act id's 'agent.op' event.</summary>
    /// <param name="actId">The act id.</param>
    /// <returns>The key.</returns>
    public static string KeyFor(ActId actId) => $"{EventTypes.AgentOp}:{actId.Value}";
}
