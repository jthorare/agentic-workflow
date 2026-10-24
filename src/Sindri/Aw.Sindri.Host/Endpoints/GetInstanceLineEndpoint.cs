using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Line;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Features.GetInstanceLine;
using Aw.Sindri.Host.Commands.Line;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// GET /sindri/workflow/instances/{instanceId}/line (line spec LN-API1; query): the LineView of a workflow.line instance, the packet chain and the phase board. The route takes
/// the instance's opaque id; the CLI resolves an address through 'GET /workflow/instances?address=' first, as for every instance read. A session caller's text is inside the
/// pack fence; a human gets it plain.
/// </summary>
internal static class GetInstanceLineEndpoint
{
    internal static RouteGroupBuilder MapGetInstanceLine(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/workflow/instances/{instanceId}/line", static async (string instanceId, IService<GetInstanceLineRequest, LineView> service, ICallerContextAccessor callers, CancellationToken ct) =>
        {
            var view = await service.Handle(new GetInstanceLineRequest(WorkflowInstanceId.From(InstanceRoute.Id(instanceId))), ct).ConfigureAwait(false);
            if (view.IsError || callers.Current?.Class != CallerClass.Session)
            {
                return view.ToHttpResult();
            }

            Result<LineView> fenced = LineViewFencing.Fence(view.Value);
            return fenced.ToHttpResult();
        });
        return group;
    }
}
