using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Failures;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Features.ListWorkflowRuns;

/// <summary>A page of a workflow's runs, newest first, whatever their status; an unknown slug is SND-GRAPH-008 (an archived workflow keeps its runs readable).</summary>
/// <param name="workflows">The workflows.</param>
/// <param name="runs">The runs.</param>
internal sealed class ListWorkflowRunsService(IWorkflowStore workflows, IWorkflowRunStore runs) : IService<ListWorkflowRunsRequest, ListWorkflowRunsResponse>
{
    /// <summary>How many runs a page holds when the call names no limit, and the most it holds when the limit is larger.</summary>
    internal const int Newest = 50;

    public async ValueTask<Result<ListWorkflowRunsResponse>> Handle(ListWorkflowRunsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var listed = await workflows.List(includeArchived: true, cancellationToken).ConfigureAwait(false);
        if (listed.IsError)
        {
            return listed.Error;
        }

        var record = listed.Value.FirstOrDefault(r => r.Slug == request.Slug);
        if (record is null)
        {
            return SindriErrors.GraphNotFound.With($"no workflow '{request.Slug.Value}'; list the workflows to see what exists");
        }

        var limit = Math.Clamp(request.Limit, 1, Newest);
        var recent = await runs.Recent(record.Id, Math.Max(request.Offset, 0), limit + 1, cancellationToken).ConfigureAwait(false);
        if (recent.IsError)
        {
            return recent.Error;
        }

        var page = recent.Value.Take(limit).ToList();
        var titles = await Titles(record.Id, page, cancellationToken).ConfigureAwait(false);
        return new ListWorkflowRunsResponse(record.Slug, [.. page.Select(r => new WorkflowRunSummary(r.Id, r.Revision, RunWords.Of(r.Status), RunWords.Kind(r.Trigger), r.StartedAt, r.EndedAt, r.Error, Sentence(r, titles)))], More: recent.Value.Count > limit);
    }

    // why: the row says why in the §6.1 words, naming the step by its title (#3304); one revision read per revision a failed run on this page walked, never one per run
    private static string? Sentence(WorkflowRun run, Dictionary<int, Dictionary<string, string>> titles) =>
        run.Status == WorkflowRunStatus.Failed && run.Error is { } error && titles.TryGetValue(run.Revision, out var named) ? StepFailures.ForRun(error, named)?.Sentence : null;

    // Each node's title (its own, else its block kind in words) per revision a failed run walked; a revision the store can't give leaves those rows with their error only.
    private async ValueTask<Dictionary<int, Dictionary<string, string>>> Titles(WorkflowId workflow, List<WorkflowRun> page, CancellationToken cancellationToken)
    {
        var titles = new Dictionary<int, Dictionary<string, string>>();
        foreach (var revision in page.Where(static r => r.Status == WorkflowRunStatus.Failed && r.Error is not null).Select(static r => r.Revision).Distinct())
        {
            var read = await workflows.GetRevision(workflow, revision, cancellationToken).ConfigureAwait(false);
            if (read.IsSuccess)
            {
                titles[revision] = read.Value.Spec.Nodes.ToDictionary(static n => n.Id.Value, static n => StepTitle.Of(n), StringComparer.Ordinal);
            }
        }

        return titles;
    }
}
