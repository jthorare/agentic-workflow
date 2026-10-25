using Aw.Context.Core.Concepts;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.GetConcepts;

/// <summary>One page of the concepts of the map the caller may read, built by <see cref="ConceptBuilder"/> (reads never call a model: the concepts are stored in the map).</summary>
internal sealed class GetConceptsService(ConceptReader reader) : IService<GetConceptsRequest, ConceptsView>
{
    public async ValueTask<Result<ConceptsView>> Handle(GetConceptsRequest request, CancellationToken cancellationToken)
    {
        var query = request.Query;
        var read = await reader.Read(request.Scope, query.Read, query.Repo, cancellationToken).ConfigureAwait(false);
        return read.IsError ? read.Error : ConceptBuilder.Build(read.Value.Basis, query, read.Value.Graph, read.Value.Text);
    }
}
