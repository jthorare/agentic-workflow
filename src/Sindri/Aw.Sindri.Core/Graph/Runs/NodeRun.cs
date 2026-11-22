using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Graph.Runs;

/// <summary>One execution of one node (table 'workflow_node_run'); a node a 'for_each' repeats has one per item.</summary>
/// <param name="RunId">The run.</param>
/// <param name="NodeId">The node.</param>
/// <param name="ItemIndex">The item's position, 0 for a node that runs once.</param>
/// <param name="Status">Where it stands.</param>
/// <param name="Input">The interpolated config as JSON, or null.</param>
/// <param name="Output">The block's output as JSON, or null.</param>
/// <param name="Truncated">True when the store cut the input or the output at <see cref="NodeRunLimits.PayloadBytes"/>.</param>
/// <param name="Error">Why it failed, or null.</param>
/// <param name="Usage">Tokens and cost as JSON, or null.</param>
/// <param name="Raw">What the model said, scrubbed by Aw.Redaction and cut at <see cref="NodeRunLimits.PayloadBytes"/>, for a prompt step whose answer failed; null otherwise (P3e, spec §6.2).</param>
public sealed record NodeRun(
    WorkflowRunId RunId,
    NodeId NodeId,
    int ItemIndex,
    NodeRunStatus Status,
    string? Input,
    string? Output,
    bool Truncated,
    string? Error,
    string? Usage,
    string? Raw = null);
