using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Features.ActivateWorkflow;
using Aw.Sindri.Features.ApplyWorkflowOps;
using Aw.Sindri.Features.CreateWorkflow;
using Aw.Sindri.Features.DeleteWorkflow;
using Aw.Sindri.Features.PauseWorkflow;
using Aw.Sindri.Features.PinWorkflowOutput;
using Aw.Sindri.Features.ReplaceWorkflow;
using Aw.Sindri.Features.TestWorkflow;
using Aw.Sindri.Features.ValidateWorkflow;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// The workflow writes (#2258, PR 8b), the routes behind 'sindri wf create|edit|validate|activate|pause|delete' and the workflow_* tools: POST /sindri/workflows, PUT and DELETE /sindri/workflows/{slug},
/// POST /sindri/workflows/{slug}/ops, /activate and /pause, and POST /sindri/workflows/validate. 'By' is the caller's actor. A stale base revision answers 409 SND-GRAPH-007 with the current revision.
/// </summary>
internal static class WorkflowWriteEndpoints
{
    internal static RouteGroupBuilder MapWorkflowWrites(this RouteGroupBuilder group)
    {
        MapCreate(group);
        MapValidate(group);
        MapOps(group);
        MapReplace(group);
        MapActivate(group);
        MapPause(group);
        MapDelete(group);
        MapTest(group);
        MapPin(group);
        return group;
    }

    private static void MapCreate(RouteGroupBuilder group)
    {
        _ = group.MapPost("/workflows", static async (CreateWorkflowBody? body, IService<CreateWorkflowRequest, CreateWorkflowResponse> service, ICallerContextAccessor callers, CancellationToken ct) =>
        {
            var slug = body?.Slug is { } text ? WorkflowReadEndpoints.Slug(text) : Missing<WorkflowSlug>("slug");
            return slug.IsError
                ? ((Result<CreateWorkflowResponse>)slug.Error).ToHttpResult()
                : (await service.Handle(new CreateWorkflowRequest(slug.Value, body!.Template, body.Spec?.ToJsonString(), By(callers)), ct).ConfigureAwait(false)).ToHttpResult();   // why: a slug was read from body, so body is not null
        });
    }

    private static void MapValidate(RouteGroupBuilder group)
    {
        _ = group.MapPost("/workflows/validate", static async (ValidateWorkflowBody? body, IService<ValidateWorkflowRequest, ValidateWorkflowResponse> service, CancellationToken ct) =>
        {
            WorkflowSlug? stored = null;
            if (body?.Slug is { } text)
            {
                var parsed = WorkflowReadEndpoints.Slug(text);
                if (parsed.IsError)
                {
                    return ((Result<ValidateWorkflowResponse>)parsed.Error).ToHttpResult();
                }

                stored = parsed.Value;
            }

            return (await service.Handle(new ValidateWorkflowRequest(stored, body?.Revision, body?.Spec?.ToJsonString()), ct).ConfigureAwait(false)).ToHttpResult();
        });
    }

    private static void MapOps(RouteGroupBuilder group)
    {
        _ = group.MapPost("/workflows/{slug}/ops", static async (string slug, ApplyWorkflowOpsBody? body, IService<ApplyWorkflowOpsRequest, ApplyWorkflowOpsResponse> service, ICallerContextAccessor callers, CancellationToken ct) =>
        {
            var parsed = WorkflowReadEndpoints.Slug(slug);
            if (parsed.IsError || body?.BaseRevision is not { } baseRevision || body.Ops is null)
            {
                return ((Result<ApplyWorkflowOpsResponse>)(parsed.IsError ? parsed.Error : SindriErrors.InvalidInput.With("send {\"baseRevision\": <n>, \"ops\": [...]}"))).ToHttpResult();
            }

            return (await service.Handle(new ApplyWorkflowOpsRequest(parsed.Value, baseRevision, body.Ops.ToJsonString(), By(callers)), ct).ConfigureAwait(false)).ToHttpResult();
        });
    }

    private static void MapReplace(RouteGroupBuilder group)
    {
        _ = group.MapPut("/workflows/{slug}", static async (string slug, ReplaceWorkflowBody? body, IService<ReplaceWorkflowRequest, ReplaceWorkflowResponse> service, ICallerContextAccessor callers, CancellationToken ct) =>
        {
            var parsed = WorkflowReadEndpoints.Slug(slug);
            if (parsed.IsError || body?.BaseRevision is not { } baseRevision || body.Spec is null)
            {
                return ((Result<ReplaceWorkflowResponse>)(parsed.IsError ? parsed.Error : SindriErrors.InvalidInput.With("send {\"baseRevision\": <n>, \"spec\": {...}}"))).ToHttpResult();
            }

            return (await service.Handle(new ReplaceWorkflowRequest(parsed.Value, baseRevision, body.Spec.ToJsonString(), By(callers)), ct).ConfigureAwait(false)).ToHttpResult();
        });
    }

    private static void MapActivate(RouteGroupBuilder group)
    {
        _ = group.MapPost("/workflows/{slug}/activate", static async (string slug, ActivateWorkflowBody? body, IService<ActivateWorkflowRequest, ActivateWorkflowResponse> service, ICallerContextAccessor callers, CancellationToken ct) =>
        {
            var parsed = WorkflowReadEndpoints.Slug(slug);
            return parsed.IsError
                ? ((Result<ActivateWorkflowResponse>)parsed.Error).ToHttpResult()
                : (await service.Handle(new ActivateWorkflowRequest(parsed.Value, body?.Revision, By(callers), body?.IfLatest == true, body?.UntestedOk == true), ct).ConfigureAwait(false)).ToHttpResult();
        });
    }

    private static void MapPause(RouteGroupBuilder group)
    {
        _ = group.MapPost("/workflows/{slug}/pause", static async (string slug, IService<PauseWorkflowRequest, PauseWorkflowResponse> service, ICallerContextAccessor callers, CancellationToken ct) =>
        {
            var parsed = WorkflowReadEndpoints.Slug(slug);
            return parsed.IsError
                ? ((Result<PauseWorkflowResponse>)parsed.Error).ToHttpResult()
                : (await service.Handle(new PauseWorkflowRequest(parsed.Value, By(callers)), ct).ConfigureAwait(false)).ToHttpResult();
        });
    }

    private static void MapDelete(RouteGroupBuilder group)
    {
        _ = group.MapDelete("/workflows/{slug}", static async (string slug, IService<DeleteWorkflowRequest, DeleteWorkflowResponse> service, ICallerContextAccessor callers, CancellationToken ct) =>
        {
            var parsed = WorkflowReadEndpoints.Slug(slug);
            return parsed.IsError
                ? ((Result<DeleteWorkflowResponse>)parsed.Error).ToHttpResult()
                : (await service.Handle(new DeleteWorkflowRequest(parsed.Value, By(callers)), ct).ConfigureAwait(false)).ToHttpResult();
        });
    }

    private static void MapTest(RouteGroupBuilder group)
    {
        _ = group.MapPost("/workflows/{slug}/test", static async (string slug, TestWorkflowBody? body, IService<TestWorkflowRequest, TestWorkflowResponse> service, ICallerContextAccessor callers, CancellationToken ct) =>
        {
            var parsed = WorkflowReadEndpoints.Slug(slug);
            if (parsed.IsError)
            {
                return ((Result<TestWorkflowResponse>)parsed.Error).ToHttpResult();
            }

            return (await service.Handle(new TestWorkflowRequest(parsed.Value, body?.Revision, body?.Trigger?.ToJsonString(), body?.TriggerEvent, Pins(body), body?.WithModel == true, body?.WithAgents == true, By(callers)), ct).ConfigureAwait(false)).ToHttpResult();
        });
    }

    private static List<TestPin>? Pins(TestWorkflowBody? body) =>
        body?.Pins is { } map ? [.. map.Select(static p => new TestPin(p.Key, p.Value?.ToJsonString() ?? "null"))] : null;

    private static void MapPin(RouteGroupBuilder group)
    {
        _ = group.MapPost("/workflows/{slug}/pins", static async (string slug, PinWorkflowOutputBody? body, IService<PinWorkflowOutputRequest, PinWorkflowOutputResponse> service, ICallerContextAccessor callers, CancellationToken ct) =>
        {
            var parsed = WorkflowReadEndpoints.Slug(slug);
            if (parsed.IsError || body?.Node is not { } node)
            {
                return ((Result<PinWorkflowOutputResponse>)(parsed.IsError ? parsed.Error : SindriErrors.InvalidInput.With("send {\"node\": <node id>, \"run\": <run id> or \"output\": {...}}"))).ToHttpResult();
            }

            WorkflowRunId? run = null;
            if (body.Run is { } runText)
            {
                if (!WorkflowRunId.TryFrom(runText, out var parsedRun))
                {
                    return ((Result<PinWorkflowOutputResponse>)SindriErrors.InvalidInput.With($"'{runText}' is not a run id")).ToHttpResult();
                }

                run = parsedRun;
            }

            return (await service.Handle(new PinWorkflowOutputRequest(parsed.Value, body.Revision, node, run, body.Output?.ToJsonString(), By(callers)), ct).ConfigureAwait(false)).ToHttpResult();
        });
    }

    private static string By(ICallerContextAccessor callers) => callers.Current?.Actor ?? "owner";

    private static Result<T> Missing<T>(string field) => SindriErrors.InvalidInput.With($"the body needs '{field}'");
}
