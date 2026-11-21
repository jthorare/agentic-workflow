using Aw.Context.Core.Concepts;
using Aw.Context.Core.History;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.GetConcepts;

/// <summary>
/// One page of the concepts of the map the caller may read, built by <see cref="ConceptBuilder"/> (reads never call a model: the concepts are stored in the map). A concept's last change is
/// the newest landed commit (git, first parent, from HEAD) among its code files; a repo git can't read adds no time, so its concepts show none rather than fail the read.
/// </summary>
internal sealed class GetConceptsService(ConceptReader reader, IFileHistory history) : IService<GetConceptsRequest, ConceptsView>
{
    // Newest first, so a file's first appearance is its last change; files untouched within this many commits show no time.
    internal const int ChangesPerRepo = 500;

    public async ValueTask<Result<ConceptsView>> Handle(GetConceptsRequest request, CancellationToken cancellationToken)
    {
        var query = request.Query;
        var read = await reader.Read(request.Scope, query.Read, query.Repo, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        var paths = ConceptBuilder.CodePaths(read.Value.Basis, query, read.Value.Graph);
        var changed = await ChangeTimes(request, read.Value.Basis, paths, cancellationToken).ConfigureAwait(false);
        return ConceptBuilder.Build(read.Value.Basis, query, read.Value.Graph, read.Value.Text, read.Value.Pins, changed);
    }

    private async ValueTask<Dictionary<string, DateTimeOffset>> ChangeTimes(GetConceptsRequest request, ConceptsBasis basis, IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        var newest = new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal);
        if (paths.Count == 0)
        {
            return newest;
        }

        var asked = new FileHistoryQuery(UpTo: null, paths, HistoryWindow.Open, ChangesPerRepo);
        foreach (var repo in request.Scope.Repos.Where(r => basis.Repo is null || string.Equals(r.Name.Value, basis.Repo, StringComparison.Ordinal)))
        {
            var answer = await history.Changes(repo, asked, cancellationToken).ConfigureAwait(false);
            if (answer.IsError)
            {
                continue;
            }

            foreach (var change in answer.Value.Changes)
            {
                foreach (var file in change.Files)
                {
                    if (!newest.TryGetValue(file, out var held) || change.At > held)
                    {
                        newest[file] = change.At;
                    }
                }
            }
        }

        return newest;
    }
}
