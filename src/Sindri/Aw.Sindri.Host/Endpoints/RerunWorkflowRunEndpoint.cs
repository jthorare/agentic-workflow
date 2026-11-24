using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Features.RerunWorkflowRun;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>POST /sindri/workflow-runs/{run}/rerun ('sindri wf rerun'): starts a new run of an ended run's revision from one step; the steps before it keep their stored output.</summary>
internal static class RerunWorkflowRunEndpoint
{
    internal static RouteGroupBuilder MapRerunWorkflowRun(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/workflow-runs/{run}/rerun", static async (string run, RerunWorkflowRunBody? body, IService<RerunWorkflowRunRequest, RerunWorkflowRunResponse> service, ICallerContextAccessor callers, CancellationToken ct) =>
        {
            if (!WorkflowRunId.TryFrom(run, out var parsed))
            {
                return ((Result<RerunWorkflowRunResponse>)SindriErrors.InvalidInput.With($"'{run}' is not a run id ('<slug>@<revision>:<occurrence>')")).ToHttpResult();
            }

            if (body is null || string.IsNullOrWhiteSpace(body.FromNode))
            {
                return ((Result<RerunWorkflowRunResponse>)SindriErrors.InvalidInput.With("a re-run needs the step to start from: {\"fromNode\": \"<node id>\"}")).ToHttpResult();
            }

            var by = callers.Current?.Actor ?? "owner";
            return (await service.Handle(new RerunWorkflowRunRequest(parsed, body.FromNode, body.Input, by), ct).ConfigureAwait(false)).ToHttpResult();
        });
        return group;
    }
}
