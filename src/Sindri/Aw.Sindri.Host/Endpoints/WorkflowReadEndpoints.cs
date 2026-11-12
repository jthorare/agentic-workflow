using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Features.GetWorkflow;
using Aw.Sindri.Features.GetWorkflowRun;
using Aw.Sindri.Features.ListTriggerEvents;
using Aw.Sindri.Features.ListWorkflowBlocks;
using Aw.Sindri.Features.ListWorkflowRuns;
using Aw.Sindri.Features.ListWorkflows;
using Aw.Sindri.Features.ListWorkflowTemplates;
using Aw.Sindri.Features.SearchWorkflowDocs;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// The workflow reads (#2258, PR 8a), the routes behind 'sindri wf list|show|runs|run-show' and the workflow_list, workflow_get, workflow_runs and workflow_run_get tools:
/// GET /sindri/workflows, /sindri/workflows/{slug}, /sindri/workflows/{slug}/runs and /sindri/workflow-runs/{run}. A slug or run id of the wrong shape is invalid input, not a 404.
/// </summary>
internal static class WorkflowReadEndpoints
{
    internal static RouteGroupBuilder MapWorkflowReads(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/workflows", static async (bool? includeArchived, IService<ListWorkflowsRequest, ListWorkflowsResponse> service, CancellationToken ct)
            => (await service.Handle(new ListWorkflowsRequest { IncludeArchived = includeArchived == true }, ct).ConfigureAwait(false)).ToHttpResult());
        // why: the literal routes below win over '/workflows/{slug}', so 'blocks' and 'docs' can't be slugs
        _ = group.MapGet("/workflows/blocks", static async (IService<ListWorkflowBlocksRequest, ListWorkflowBlocksResponse> service, CancellationToken ct)
            => (await service.Handle(new ListWorkflowBlocksRequest(), ct).ConfigureAwait(false)).ToHttpResult());
        _ = group.MapGet("/workflows/templates", static async (IService<ListWorkflowTemplatesRequest, ListWorkflowTemplatesResponse> service, CancellationToken ct)
            => (await service.Handle(new ListWorkflowTemplatesRequest(), ct).ConfigureAwait(false)).ToHttpResult());
        _ = group.MapGet("/workflows/trigger-events", static async (string? kind, string? connection, IService<ListTriggerEventsRequest, ListTriggerEventsResponse> service, CancellationToken ct)
            => string.IsNullOrWhiteSpace(kind)
                ? ((Result<ListTriggerEventsResponse>)SindriErrors.InvalidInput.With("send ?kind=<event kind>, such as github.pr.opened")).ToHttpResult()
                : (await service.Handle(new ListTriggerEventsRequest(kind, connection), ct).ConfigureAwait(false)).ToHttpResult());
        _ = group.MapGet("/workflows/docs", static async (string? question, string? page, IService<SearchWorkflowDocsRequest, SearchWorkflowDocsResponse> service, CancellationToken ct)
            => (await service.Handle(new SearchWorkflowDocsRequest(question, page), ct).ConfigureAwait(false)).ToHttpResult());
        _ = group.MapGet("/workflows/{slug}", static async (string slug, IService<GetWorkflowRequest, GetWorkflowResponse> service, CancellationToken ct) =>
        {
            var parsed = Slug(slug);
            return parsed.IsError ? ((Result<GetWorkflowResponse>)parsed.Error).ToHttpResult() : (await service.Handle(new GetWorkflowRequest(parsed.Value), ct).ConfigureAwait(false)).ToHttpResult();
        });
        _ = group.MapGet("/workflows/{slug}/runs", static async (string slug, IService<ListWorkflowRunsRequest, ListWorkflowRunsResponse> service, CancellationToken ct) =>
        {
            var parsed = Slug(slug);
            return parsed.IsError ? ((Result<ListWorkflowRunsResponse>)parsed.Error).ToHttpResult() : (await service.Handle(new ListWorkflowRunsRequest(parsed.Value), ct).ConfigureAwait(false)).ToHttpResult();
        });
        _ = group.MapGet("/workflow-runs/{run}", static async (string run, IService<GetWorkflowRunRequest, GetWorkflowRunResponse> service, CancellationToken ct) =>
        {
            var parsed = Run(run);
            return parsed.IsError ? ((Result<GetWorkflowRunResponse>)parsed.Error).ToHttpResult() : (await service.Handle(new GetWorkflowRunRequest(parsed.Value), ct).ConfigureAwait(false)).ToHttpResult();
        });
        return group;
    }

    /// <summary>Binds a workflow slug from a route value.</summary>
    /// <param name="slug">The route value.</param>
    /// <returns>The slug, or SND-API invalid input.</returns>
    internal static Result<WorkflowSlug> Slug(string slug) =>
        WorkflowSlug.TryFrom(slug, out var parsed) ? parsed : SindriErrors.InvalidInput.With($"'{slug}' is not a workflow slug (lowercase letters, digits and '-')");

    /// <summary>Binds a run id from a route value.</summary>
    /// <param name="run">The route value.</param>
    /// <returns>The run id, or SND-API invalid input.</returns>
    internal static Result<WorkflowRunId> Run(string run) =>
        WorkflowRunId.TryFrom(run, out var parsed) ? parsed : SindriErrors.InvalidInput.With($"'{run}' is not a run id ('<slug>@<revision>:<occurrence>')");
}
