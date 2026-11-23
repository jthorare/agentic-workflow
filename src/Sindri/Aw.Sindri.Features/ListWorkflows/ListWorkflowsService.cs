using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Features.ListWorkflows;

/// <summary>The workflows that are not archived (or, asked, all of them with the archived ones marked), by slug, each with who saved its newest revision.</summary>
/// <param name="workflows">The workflows and their revisions.</param>
/// <param name="runs">The runs behind the last-run chip and the strip (issue 3683).</param>
/// <param name="clock">The time the next run is measured from (issue 3683).</param>
internal sealed class ListWorkflowsService(IWorkflowStore workflows, IWorkflowRunStore runs, TimeProvider clock) : IService<ListWorkflowsRequest, ListWorkflowsResponse>
{
    /// <summary>How many of a workflow's newest runs the row's strip holds.</summary>
    internal const int StripLength = 10;

    public async ValueTask<Result<ListWorkflowsResponse>> Handle(ListWorkflowsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var listed = await workflows.List(request.IncludeArchived, cancellationToken).ConfigureAwait(false);
        if (listed.IsError)
        {
            return listed.Error;
        }

        var rows = new List<WorkflowSummary>(listed.Value.Count);
        foreach (var record in listed.Value)
        {
            var newest = await workflows.GetRevision(record.Id, record.LatestRevision, cancellationToken).ConfigureAwait(false);
            if (newest.IsError)
            {
                return newest.Error;
            }

            var row = await Row(record, newest.Value, cancellationToken).ConfigureAwait(false);
            if (row.IsError)
            {
                return row.Error;
            }

            rows.Add(row.Value);
        }

        return new ListWorkflowsResponse(rows);
    }

    // why: the trigger shown is the active revision's (what runs), else the newest; the strip is the newest runs of any status
    private async ValueTask<Result<WorkflowSummary>> Row(WorkflowRecord record, WorkflowRevision newest, CancellationToken cancellationToken)
    {
        var shown = record.ActiveRevision is { } active && active != record.LatestRevision ? await workflows.GetRevision(record.Id, active, cancellationToken).ConfigureAwait(false) : newest;
        if (shown.IsError)
        {
            return shown.Error;
        }

        var recent = await runs.Recent(record.Id, 0, StripLength, cancellationToken).ConfigureAwait(false);
        if (recent.IsError)
        {
            return recent.Error;
        }

        var trigger = TriggerWords.Trigger(shown.Value.Spec);
        var strip = recent.Value.Select(static r => new WorkflowRecentRun(r.Id, RunWords.Of(r.Status), r.StartedAt, RunWords.Seconds(r.StartedAt, r.EndedAt))).ToList();
        return new WorkflowSummary(record.Slug, record.Name, record.ActiveRevision, record.LatestRevision, newest.CreatedBy, newest.CreatedAt)
        {
            Archived = record.ArchivedAt is null ? null : true,
            Trigger = TriggerWords.Of(trigger),
            Paused = record.WasActivated && record.ActiveRevision is null,
            NextRunAt = record.ActiveRevision is null ? null : TriggerWords.Next(trigger, clock.GetUtcNow()),
            LastRun = recent.Value.Count == 0 ? null : new WorkflowLastRun(recent.Value[0].Id, strip[0].Status, recent.Value[0].StartedAt),
            Recent = strip,
        };
    }
}
