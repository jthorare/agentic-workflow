using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Map;
using Aw.Context.Core.Pages;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// One slice of the pages of the repo the caller may read, through the engine (which gates and scopes sessions). A concept or repo the text cannot name is the same
/// SND-CONTEXT-258 the engine answers for one the map lacks; a retained version or the draft that is missing still refuses.
/// </summary>
internal sealed class GetContextPagesService(ContextScopes scopes, IContextEngine engine) : IService<GetContextPagesRequest, PagesView>
{
    private const string ConceptPrefix = "concept:";

    public async ValueTask<Result<PagesView>> Handle(GetContextPagesRequest request, CancellationToken cancellationToken)
    {
        var read = MapRead.Of(request.Draft, request.MapVersion);
        if (read.IsError)
        {
            return read.Error;
        }

        var concept = Concept(request.Concept);
        if (concept.IsError)
        {
            return concept.Error;
        }

        var repo = Repo(request.Repo);
        if (repo.IsError)
        {
            return repo.Error;
        }

        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        if (scope.IsError)
        {
            return scope.Error;
        }

        var query = new PagesQuery(concept.Value, request.Page, repo.Value, request.Marks, request.Limit, request.Offset, request.Draft, request.MapVersion);
        return await engine.Pages(query, scope.Value, cancellationToken).ConfigureAwait(false);
    }

    private static Result<ConceptId?> Concept(string? text)
    {
        if (text is null)
        {
            return (ConceptId?)null;
        }

        var bare = text.StartsWith(ConceptPrefix, StringComparison.Ordinal) ? text[ConceptPrefix.Length..] : text;
        return ConceptId.TryFrom(bare, out var id) ? id : ContextErrors.PageQueryUnknown.With($"context_pages: no concept '{text}' (fix: sindri context map status lists the concepts)");
    }

    internal static Result<ContextRepoName?> Repo(string? text)
    {
        if (text is null)
        {
            return (ContextRepoName?)null;
        }

        return ContextRepoName.TryFrom(text, out var name) ? name : ContextErrors.PageQueryUnknown.With($"context_pages: no repo '{text}' (fix: name a repo of the profile)");
    }
}
