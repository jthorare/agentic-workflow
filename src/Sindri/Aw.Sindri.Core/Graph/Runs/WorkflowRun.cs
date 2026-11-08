using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Graph.Runs;

/// <summary>One run of one workflow revision (table 'workflow_run').</summary>
/// <param name="Id">'&lt;slug&gt;@&lt;revision&gt;:&lt;occurrence&gt;'; also the Temporal workflow id.</param>
/// <param name="WorkflowId">The workflow.</param>
/// <param name="Revision">The revision the run walks.</param>
/// <param name="Trigger">What started it, as JSON ('{"kind":"manual"}').</param>
/// <param name="Status">Where it stands.</param>
/// <param name="Outputs">Its outputs as JSON, or null while none.</param>
/// <param name="Error">Why it failed, or null.</param>
/// <param name="ItemId">The item it works for, or null.</param>
/// <param name="StartedAt">When the run was recorded.</param>
/// <param name="EndedAt">When it reached a final status, or null.</param>
public sealed record WorkflowRun(
    WorkflowRunId Id,
    WorkflowId WorkflowId,
    int Revision,
    string Trigger,
    WorkflowRunStatus Status,
    string? Outputs,
    string? Error,
    ItemId? ItemId,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt);
