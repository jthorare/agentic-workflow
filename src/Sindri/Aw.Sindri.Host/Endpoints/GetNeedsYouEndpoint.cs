using Aw.Kernel.Services;
using Aw.Sindri.Features.GetNeedsYou;

namespace Aw.Sindri.Host.Endpoints;

internal static class GetNeedsYouEndpoint
{
    internal static RouteGroupBuilder MapGetNeedsYou(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/needs-you", static async (HttpContext http, IService<GetNeedsYouRequest, GetNeedsYouResponse> service, CancellationToken ct) =>
        {
            var ifNoneMatch = http.Request.Headers.IfNoneMatch.ToString().Trim('"');
            var result = await service.Handle(new GetNeedsYouRequest(ifNoneMatch.Length == 0 ? null : ifNoneMatch), ct).ConfigureAwait(false);
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
