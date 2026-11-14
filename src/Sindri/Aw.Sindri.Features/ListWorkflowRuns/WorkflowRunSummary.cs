using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Features.ListWorkflowRuns;

/// <summary>One run in the list.</summary>
/// <param name="Id">The run.</param>
/// <param name="Revision">The revision it walked.</param>
/// <param name="Status">'queued', 'running', 'waiting', 'succeeded', 'failed' or 'cancelled'.</param>
/// <param name="Trigger">What started it: 'manual', 'schedule', 'cli' or 'agent' (a stored 'manual' trigger by 'session:&lt;id&gt;' is 'agent', by 'cli' is 'cli'); any other stored kind as it is.</param>
/// <param name="StartedAt">When it was recorded.</param>
/// <param name="EndedAt">When it ended, or null while it has not.</param>
/// <param name="Error">Why it failed, or null (all of it; <see cref="Result"/> is the one-line form).</param>
internal sealed record WorkflowRunSummary(WorkflowRunId Id, int Revision, string Status, string Trigger, DateTimeOffset StartedAt, DateTimeOffset? EndedAt, string? Error)
{
    /// <summary>How long the run took in seconds (EndedAt - StartedAt); null while it has not ended. Issue 3683.</summary>
    public double? Seconds { get; init; }

    /// <summary>One line for the table: the first line of the error for a failed or cancelled run; for a succeeded run its outputs as compact JSON cut at 120 characters with a trailing '…'; null when there is nothing to say. Issue 3683.</summary>
    public string? Result { get; init; }
}
