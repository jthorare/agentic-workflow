using Aw.CodeIndex.Core.Facade;
using Aw.Kernel.Services;
using Aw.Sindri.Features.CodeIndexReads;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>GET /sindri/code-index/symbols?query=&amp;repo=&amp;match=&amp;concept=: symbols by name, path, path#name or ref (exact), or by name prefix.</summary>
internal static class FindCodeSymbolsEndpoint
{
    internal static RouteGroupBuilder MapFindCodeSymbols(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/code-index/symbols", static async (string? query, string? repo, string? match, string? concept, IService<FindCodeSymbolsRequest, FindSymbolsAnswer> symbols, CancellationToken ct) =>
        {
            var answer = await symbols.Handle(new FindCodeSymbolsRequest(query ?? string.Empty, repo, match, concept), ct).ConfigureAwait(false);
            return answer.IsError ? HttpResults.Problem(answer.Error) : TypedResults.Ok(answer.Value);
        });
        return group;
    }
}
