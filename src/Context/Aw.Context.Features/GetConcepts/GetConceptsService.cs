using Aw.Context.Core.Concepts;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.GetConcepts;

/// <summary>
/// One page of the concepts of the map the caller may read, built by <see cref="ConceptBuilder"/> (reads never call a model: the concepts are stored in the map). A concept's last change
/// comes from <see cref="ConceptChangeTimes"/>, a cache the read never waits on: a cold or stale cache answers without it and fills in the background.
/// </summary>
internal sealed class GetConceptsService(ConceptReader reader, ConceptChangeTimes times) : IService<GetConceptsRequest, ConceptsView>
{
    public async ValueTask<Result<ConceptsView>> Handle(GetConceptsRequest request, CancellationToken cancellationToken)
    {
        var query = request.Query;
        var read = await reader.Read(request.Scope, query.Read, query.Repo, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        var basis = read.Value.Basis;
        var paths = ConceptBuilder.CodePaths(basis, query, read.Value.Graph);
        var repos = request.Scope.Repos.Where(r => basis.Repo is null || string.Equals(r.Name.Value, basis.Repo, StringComparison.Ordinal));
        var changed = await times.Read(repos, paths, cancellationToken).ConfigureAwait(false);
        return ConceptBuilder.Build(basis with { Changed = changed }, query, read.Value.Graph, read.Value.Text, read.Value.Pins);
    }
}
