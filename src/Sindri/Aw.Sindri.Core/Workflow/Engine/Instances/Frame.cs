using System.Text.Json.Serialization;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Workflow.Engine.Instances;

/// <summary>
/// The recipe frame (spec §5.1, rev 8): the state the instance is in and what a resume must find again.
/// <see cref="Entry"/> rises on each real state entry. A park, escalation or takeover sits beside the frame as the
/// instance's <see cref="WorkflowInstance.Suspension"/> and leaves the frame as it was. Timers are the interpreter's
/// (§5.5), so the frame keeps none.
/// </summary>
/// <param name="State">A recipe state, or 'cancelled' once the instance was cancelled (<see cref="EngineStates.Cancelled"/>).</param>
/// <param name="Entry">The entry number, from 1.</param>
/// <param name="Run">The open Step run, in an active state.</param>
/// <param name="Child">The child this frame's child wait waits on.</param>
public sealed record Frame(string State, int Entry, OpenRun? Run, WorkflowInstanceId? Child)
{
    /// <summary>
    /// The recipe transition (its index) that entered this frame; null for the initial state. A 'then: retry' re-emits
    /// its effects under the same identities (spec §3.4, §5.7).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? EnteredBy { get; init; }

    /// <summary>
    /// Whether this entry used its one 'then: retry' (spec §3.4, §5.5): the interpreter then runs the retry timer, and
    /// any later expiry in the entry parks with 'timeout'.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Retried { get; init; }
}
