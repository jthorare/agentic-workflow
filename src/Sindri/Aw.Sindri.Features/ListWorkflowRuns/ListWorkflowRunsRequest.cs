using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Features.ListWorkflowRuns;

/// <summary>List a workflow's newest runs (GET /sindri/workflows/{slug}/runs, 'sindri wf runs', MCP workflow_runs).</summary>
/// <param name="Slug">The workflow.</param>
/// <param name="Offset">How many of the newest runs to skip (0 when the call pages no further).</param>
/// <param name="Limit">How many runs to return, at most; <see cref="ListWorkflowRunsService.Newest"/> when the call names none.</param>
internal sealed record ListWorkflowRunsRequest(WorkflowSlug Slug, int Offset = 0, int Limit = ListWorkflowRunsService.Newest) : IQuery<ListWorkflowRunsResponse>
{
    /// <summary>Keep only runs with this status word ('queued', 'running', 'waiting', 'succeeded', 'failed', 'cancelled'); any other word is invalid input. Null keeps every status. Issue 3683.</summary>
    public string? Status { get; init; }

    /// <summary>Keep only runs started at or after this instant (inclusive). Issue 3683.</summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>Keep only runs started before this instant (exclusive). Issue 3683.</summary>
    public DateTimeOffset? To { get; init; }
}
