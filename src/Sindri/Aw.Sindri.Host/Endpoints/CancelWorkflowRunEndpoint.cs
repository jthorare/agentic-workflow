using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Features.CancelWorkflowRun;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>POST /sindri/workflow-runs/{run}/cancel ('sindri wf cancel'): asks a run in flight to stop.</summary>
internal static class CancelWorkflowRunEndpoint
{
    internal static RouteGroupBuilder MapCancelWorkflowRun(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/workflow-runs/{run}/cancel", static async (string run, IService<CancelWorkflowRunRequest, CancelWorkflowRunResponse> service, ICallerContextAccessor callers, CancellationToken ct) =>
        {
            if (!WorkflowRunId.TryFrom(run, out var parsed))
            {
                return ((Result<CancelWorkflowRunResponse>)SindriErrors.InvalidInput.With($"'{run}' is not a run id ('<slug>@<revision>:<occurrence>')")).ToHttpResult();
            }

            var by = callers.Current?.Actor ?? "owner";
            return (await service.Handle(new CancelWorkflowRunRequest(parsed, by), ct).ConfigureAwait(false)).ToHttpResult();
        });
        return group;
    }
}
