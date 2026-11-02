using Aw.Context.Core.Concepts;
using Aw.Kernel.Services;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>GET /sindri/context/concepts/glossary?repo=&amp;draft=&amp;mapVersion=: the words that characterize each concept, and for each word the concepts it characterizes.</summary>
internal static class GetContextConceptGlossaryEndpoint
{
    internal static RouteGroupBuilder MapGetContextConceptGlossary(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/context/concepts/glossary", static async (string? repo, bool? draft, string? mapVersion, IService<GetContextConceptGlossaryRequest, ConceptGlossaryView> service, CancellationToken ct)
            => (await service.Handle(new GetContextConceptGlossaryRequest(repo, draft ?? false, mapVersion), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
