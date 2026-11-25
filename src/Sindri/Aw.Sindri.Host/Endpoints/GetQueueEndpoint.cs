using Aw.Kernel.Services;
using Aw.Sindri.Features.GetQueue;

namespace Aw.Sindri.Host.Endpoints;

internal static class GetQueueEndpoint
{
    internal static RouteGroupBuilder MapGetQueue(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/queue", static async (HttpContext http, IService<GetQueueRequest, QueueResponse> service, int? limit, string? group, string? after, CancellationToken ct) =>
        {
            var ifNoneMatch = http.Request.Headers.IfNoneMatch.ToString().Trim('"');
            var result = await service.Handle(new GetQueueRequest(limit, group, after, ifNoneMatch.Length == 0 ? null : ifNoneMatch), ct).ConfigureAwait(false);
            if (result.IsSuccess && result.Value.Tag is { } tag)
            {
                http.Response.Headers.ETag = $"\"{tag}\"";
                if (result.Value.NotModified)
                {
                    return Results.StatusCode(StatusCodes.Status304NotModified);
                }
            }

            return result.ToHttpResult();
        });
        return group;
    }
}
