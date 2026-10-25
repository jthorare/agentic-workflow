using Aw.Kernel.Services;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// GET /sindri/context/lenses?anchor=&amp;depth=&amp;hops=&amp;draft=&amp;mapVersion=&amp;limit=&amp;offset=: one distinct anchor answers schema 1, a repeated 'anchor' of several answers schema 2. QUERY /sindri/context/lenses takes the same fields as the
/// JSON body ({anchors[], depth?, hops?, draft?, mapVersion?, limit?, offset?}) for a list too long for the request line, through the same service (see GetContextSubgraphEndpoint for the method).
/// </summary>
internal static class GetContextLensesEndpoint
{
    internal static RouteGroupBuilder MapGetContextLenses(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/context/lenses", static async (string[]? anchor, int? depth, bool? draft, string? mapVersion, string? hops, int? limit, int? offset, IService<GetContextLensesAnswerRequest, LensesAnswer> lenses, CancellationToken ct) =>
        {
            var answer = await lenses.Handle(new GetContextLensesAnswerRequest(anchor ?? [], draft ?? false, depth, mapVersion, hops, limit, offset), ct).ConfigureAwait(false);
            return answer.IsError ? HttpResults.Problem(answer.Error) : answer.Value.Many is { } many ? TypedResults.Ok(many) : TypedResults.Ok(answer.Value.One);
        });
        _ = group.MapGroup(string.Empty).WithMetadata(BodyQueryRouteMetadata.Instance).MapMethods("/context/lenses", [HttpMethods.Query], static async (GetContextLensesAnswerRequest request, IService<GetContextLensesAnswerRequest, LensesAnswer> lenses, CancellationToken ct) =>
        {
            var answer = await lenses.Handle(request with { Anchors = request.Anchors ?? [] }, ct).ConfigureAwait(false);
            return answer.IsError ? HttpResults.Problem(answer.Error) : answer.Value.Many is { } many ? TypedResults.Ok(many) : TypedResults.Ok(answer.Value.One);
        });
        return group;
    }
}
