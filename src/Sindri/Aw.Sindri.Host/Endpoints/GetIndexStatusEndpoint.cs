using Aw.Kernel.Services;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>GET /sindri/index/status: where each record source's sync stands (owned-index spec §5.8). Any caller.</summary>
internal static class GetIndexStatusEndpoint
{
    internal static RouteGroupBuilder MapGetIndexStatus(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/index/status", static async (IService<GetIndexStatusRequest, IndexStatusView> status, CancellationToken ct) =>
        {
            var answer = await status.Handle(new GetIndexStatusRequest(), ct).ConfigureAwait(false);
            return answer.IsError ? HttpResults.Problem(answer.Error) : TypedResults.Ok(answer.Value);
        });
        return group;
    }
}
