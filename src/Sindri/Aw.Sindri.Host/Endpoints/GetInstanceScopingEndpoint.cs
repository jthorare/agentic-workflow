using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Features.Scoping;
using Aw.Sindri.Host.Commands.Scoping;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// GET /sindri/workflow/instances/{instanceId}/scoping (scoping spec API2; query): the ScopingView of a workflow.scoping instance, live before
/// evidence. The route takes the instance's opaque id; the CLI resolves an address through 'GET /workflow/instances?address=' first, as for every
/// instance read. A session caller's text is inside the pack fence (SEC M3); a human gets it plain.
/// </summary>
internal static class GetInstanceScopingEndpoint
{
    internal static RouteGroupBuilder MapGetInstanceScoping(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/workflow/instances/{instanceId}/scoping", static async (string instanceId, IService<GetInstanceScopingRequest, ScopingView> service, ICallerContextAccessor callers, CancellationToken ct) =>
        {
            var view = await service.Handle(new GetInstanceScopingRequest(WorkflowInstanceId.From(InstanceRoute.Id(instanceId))), ct).ConfigureAwait(false);
            if (view.IsError || callers.Current?.Class != CallerClass.Session)
            {
                return view.ToHttpResult();
            }

            Result<ScopingView> fenced = ScopingFencing.Fence(view.Value);
            return fenced.ToHttpResult();
        });
        return group;
    }
}
