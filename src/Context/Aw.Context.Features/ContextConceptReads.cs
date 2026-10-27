using Aw.Context.Core.Concepts;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Features.GetConceptGlossary;
using Aw.Context.Features.GetConceptMembers;
using Aw.Context.Features.GetConcepts;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features;

/// <summary>The concept layer's reads of the facade: the concepts, one concept's members and the glossary, each dispatched to its slice.</summary>
internal sealed class ContextConceptReads(
    IService<GetConceptsRequest, ConceptsView> concepts,
    IService<GetConceptMembersRequest, ConceptMembersView> members,
    IService<GetConceptGlossaryRequest, ConceptGlossaryView> glossary) : IContextConcepts
{
    public ValueTask<Result<ConceptsView>> Concepts(ConceptsQuery query, ContextScope scope, CancellationToken cancellationToken) =>
        concepts.Handle(new GetConceptsRequest(query, scope), cancellationToken);

    public ValueTask<Result<ConceptMembersView>> ConceptMembers(ConceptMembersQuery query, ContextScope scope, CancellationToken cancellationToken) =>
        members.Handle(new GetConceptMembersRequest(query, scope), cancellationToken);

    public ValueTask<Result<ConceptGlossaryView>> ConceptGlossary(ConceptGlossaryQuery query, ContextScope scope, CancellationToken cancellationToken) =>
        glossary.Handle(new GetConceptGlossaryRequest(query, scope), cancellationToken);
}
