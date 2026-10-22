using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.Kernel;

namespace Aw.CodeIndex.Features;

public sealed partial class CodeIndexFacade
{
    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<LayerProbe>>> ProbeLayers(CodeIndexScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var probes = new List<LayerProbe>(scope.Repos.Count);
        foreach (var repo in scope.Repos)
        {
            var buildable = new List<LayerName>();
            if (graph is not null && repo.Settings.Graph.Enabled && await GraphWasUnavailable(repo, cancellationToken).ConfigureAwait(false)
                && (await graph.Probe(cancellationToken).ConfigureAwait(false)).IsSuccess)
            {
                buildable.Add(LayerName.Graph);
            }

            probes.Add(new LayerProbe(repo.Name, buildable));
        }

        return probes;
    }

    private async ValueTask<bool> GraphWasUnavailable(CodeIndexRepo repo, CancellationToken cancellationToken)
    {
        var header = await store.ReadHeader(repo.Name, cancellationToken).ConfigureAwait(false);
        return header is { IsError: false, Value: { } live } && live.Layers.Any(l => l.Layer == LayerName.Graph && l.State == LayerState.Unavailable);
    }
}
