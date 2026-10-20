using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Line;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Features.Design;
using Aw.Sindri.Host.Commands.Line;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// GET /sindri/workflow/instances/{instanceId}/design-stop (line spec LN-API2; query): the DesignStopView of a workflow.plan instance, live before evidence. The route takes
/// the instance's opaque id; the CLI resolves an address through 'GET /workflow/instances?address=' first, as for every instance read. A session caller's text is inside the
/// pack fence; a human gets it plain.
/// </summary>
internal static class GetInstanceDesignStopEndpoint
{
    internal static RouteGroupBuilder MapGetInstanceDesignStop(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/workflow/instances/{instanceId}/design-stop", static async (string instanceId, IService<GetInstanceDesignStopRequest, DesignStopView> service, ICallerContextAccessor callers, CancellationToken ct) =>
        {
            var view = await service.Handle(new GetInstanceDesignStopRequest(WorkflowInstanceId.From(InstanceRoute.Id(instanceId))), ct).ConfigureAwait(false);
            if (view.IsError || callers.Current?.Class != CallerClass.Session)
            {
                return view.ToHttpResult();
            }

            Result<DesignStopView> fenced = DesignStopFencing.Fence(view.Value);
            return fenced.ToHttpResult();
        });
        return group;
    }
}
