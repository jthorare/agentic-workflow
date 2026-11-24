using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Features.MarkWorkflowRunSeen;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>POST /sindri/workflow-runs/{run}/seen: the owner opened a failed run, so its Home row clears.</summary>
internal static class MarkWorkflowRunSeenEndpoint
{
    internal static RouteGroupBuilder MapMarkWorkflowRunSeen(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/workflow-runs/{run}/seen", static async (string run, IService<MarkWorkflowRunSeenRequest, MarkWorkflowRunSeenResponse> service, CancellationToken ct) =>
            WorkflowRunId.TryFrom(run, out var parsed)
                ? (await service.Handle(new MarkWorkflowRunSeenRequest(parsed), ct).ConfigureAwait(false)).ToHttpResult()
                : ((Result<MarkWorkflowRunSeenResponse>)SindriErrors.InvalidInput.With($"'{run}' is not a run id ('<slug>@<revision>:<occurrence>')")).ToHttpResult());
        return group;
    }
}
