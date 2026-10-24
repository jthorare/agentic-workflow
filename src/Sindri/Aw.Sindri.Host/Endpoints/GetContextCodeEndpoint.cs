using Aw.Context.Core.Code;
using Aw.Kernel.Services;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>GET /sindri/context/code?anchor=&amp;draft=&amp;mapVersion=: the files, tests, data things and schema files around one or more anchors (a repeated 'anchor').</summary>
internal static class GetContextCodeEndpoint
{
    internal static RouteGroupBuilder MapGetContextCode(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/context/code", static async (string[]? anchor, bool? draft, string? mapVersion, IService<GetContextCodeRequest, CodeView> code, CancellationToken ct) =>
        {
            var answer = await code.Handle(new GetContextCodeRequest(anchor ?? [], draft ?? false, mapVersion), ct).ConfigureAwait(false);
            return answer.IsError ? HttpResults.Problem(answer.Error) : TypedResults.Ok(answer.Value);
        });
        return group;
    }
}
