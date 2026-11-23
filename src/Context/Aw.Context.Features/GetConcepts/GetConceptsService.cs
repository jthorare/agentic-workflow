using Aw.Context.Core.Concepts;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.GetConcepts;

/// <summary>
/// One page of the concepts of the map the caller may read: the answer kept for this map version and caller, or built by <see cref="ConceptBuilder"/> and kept (issue 3676; reads
/// never call a model: the concepts are stored in the map). A concept's last change comes from <see cref="ConceptChangeTimes"/>, a cache the read never waits on (#3760). It is read
/// outside the kept answer's key, so it is laid over the kept rows on every read and never stored: a time git learns after the answer was kept still shows.
/// </summary>
internal sealed class GetConceptsService(ConceptAnswers answers, ConceptChangeTimes times) : IService<GetConceptsRequest, ConceptsView>
{
    public async ValueTask<Result<ConceptsView>> Handle(GetConceptsRequest request, CancellationToken cancellationToken)
    {
        var kept = await answers.Concepts(request.Scope, request.Query, cancellationToken).ConfigureAwait(false);
        if (kept.IsError)
        {
            return kept.Error;
        }

        var repo = kept.Value.View.Repo;
        var repos = request.Scope.Repos.Where(r => repo is null || string.Equals(r.Name.Value, repo, StringComparison.Ordinal));
        var changed = await times.Read(repos, kept.Value.Paths, cancellationToken).ConfigureAwait(false);
        return kept.Value.With(changed);
    }
}
