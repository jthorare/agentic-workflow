using Aw.Context.Core;
using Aw.Context.Core.Concepts;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>One page of one concept's members, through the engine (which gates and scopes sessions).</summary>
internal sealed class GetContextConceptMembersService(ContextScopes scopes, IContextEngine engine) : IService<GetContextConceptMembersRequest, ConceptMembersView>
{
    public async ValueTask<Result<ConceptMembersView>> Handle(GetContextConceptMembersRequest request, CancellationToken cancellationToken)
    {
        var read = MapRead.Of(request.Draft, request.MapVersion);
        if (read.IsError)
        {
            return read.Error;
        }

        if (!NodeKey.TryFrom(request.Concept, out var concept) || concept.Kind != NodeKind.Concept)
        {
            return ContextErrors.UnknownAnchor.With("that concept isn't in this map (fix: read the concepts first, and name one of their ids)");
        }

        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        return scope.IsError
            ? scope.Error
            : await engine.ConceptMembers(new ConceptMembersQuery(concept, read.Value, new ConceptPage(request.Cursor, request.Limit), request.Repo), scope.Value, cancellationToken).ConfigureAwait(false);
    }
}
