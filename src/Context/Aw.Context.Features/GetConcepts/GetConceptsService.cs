using Aw.Context.Core.Concepts;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.GetConcepts;

/// <summary>One page of the concepts of the map the caller may read: the answer kept for this map version and caller, or built by <see cref="ConceptBuilder"/> and kept (reads never call a model: the concepts are stored in the map).</summary>
internal sealed class GetConceptsService(ConceptAnswers answers) : IService<GetConceptsRequest, ConceptsView>
{
    public async ValueTask<Result<ConceptsView>> Handle(GetConceptsRequest request, CancellationToken cancellationToken)
    {
        var kept = await answers.Concepts(request.Scope, request.Query, cancellationToken).ConfigureAwait(false);

        // The overlay seam (issue 3676 with #3760): the kept answer holds only what the map version and the profile decide, so it can be kept by ConceptAnswerKey. Anything read
        // from outside that key (git change times per concept, read from a cache that fills in the background) is laid over the rows HERE, after the lookup, on every read,
        // and never stored, so a kept answer never freezes it.
        return kept;
    }
}
