using Aw.Context.Core.Concepts;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.GetConceptMembers;

/// <summary>One page of one concept's members in the map the caller may read.</summary>
internal sealed class GetConceptMembersService(ConceptReader reader) : IService<GetConceptMembersRequest, ConceptMembersView>
{
    public async ValueTask<Result<ConceptMembersView>> Handle(GetConceptMembersRequest request, CancellationToken cancellationToken)
    {
        var query = request.Query;
        var read = await reader.Read(request.Scope, query.Read, query.Repo, cancellationToken).ConfigureAwait(false);
        return read.IsError ? read.Error : ConceptBuilder.Members(read.Value.Basis, query, read.Value.Graph, read.Value.Text);
    }
}
