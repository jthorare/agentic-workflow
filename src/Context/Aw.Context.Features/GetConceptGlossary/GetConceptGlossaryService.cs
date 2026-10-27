using Aw.Context.Core.Concepts;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.GetConceptGlossary;

/// <summary>The glossary of the map the caller may read, built by <see cref="ConceptGlossaryBuilder"/> from the terms stored on the concepts (reads never call a model or the code index).</summary>
internal sealed class GetConceptGlossaryService(ConceptReader reader) : IService<GetConceptGlossaryRequest, ConceptGlossaryView>
{
    public async ValueTask<Result<ConceptGlossaryView>> Handle(GetConceptGlossaryRequest request, CancellationToken cancellationToken)
    {
        var query = request.Query;
        var read = await reader.Read(request.Scope, query.Read, query.Repo, cancellationToken).ConfigureAwait(false);
        return read.IsError ? read.Error : ConceptGlossaryBuilder.Build(read.Value.Basis, read.Value.Graph, read.Value.Text);
    }
}
