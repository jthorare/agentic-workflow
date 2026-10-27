using Aw.Context.Core;
using Aw.Context.Core.Concepts;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// One page of the concepts of the repo the caller may read, through the engine (which gates and scopes sessions). No map yet is an answer, not an error (state 'building' or 'unavailable'), so the
/// top band can say so; a retained version or the draft that is missing still refuses.
/// </summary>
internal sealed class GetContextConceptsService(ContextScopes scopes, IContextEngine engine, ConceptsNotReady notReady) : IService<GetContextConceptsRequest, ConceptsView>
{
    public async ValueTask<Result<ConceptsView>> Handle(GetContextConceptsRequest request, CancellationToken cancellationToken)
    {
        var read = MapRead.Of(request.Draft, request.MapVersion);
        if (read.IsError)
        {
            return read.Error;
        }

        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        if (scope.IsError)
        {
            return scope.Error;
        }

        var query = new ConceptsQuery(read.Value, new ConceptPage(request.Cursor, request.Limit), request.Repo, request.Q);
        var answer = await engine.Concepts(query, scope.Value, cancellationToken).ConfigureAwait(false);
        return answer.IsError && !request.Draft && request.MapVersion is null && string.Equals(answer.Error.Code, ContextErrors.NoMap.Code, StringComparison.Ordinal)
            ? await notReady.Explain(answer.Error, scope.Value, cancellationToken).ConfigureAwait(false)
            : answer;
    }
}
