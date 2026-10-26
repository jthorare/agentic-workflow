using Aw.Context.Core.Engine;
using Aw.Context.Core.LensSupport;
using Aw.Context.Core.Map;
using Aw.Context.Core.Subgraph;
using Aw.Context.Core.Trust;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.GetSubgraph;

/// <summary>
/// The subgraph around a set of node keys (scoping spec C14): one snapshot of the map the caller may read (a session's graph is already cut to its repo, and a deny glob hides its
/// nodes), as of the current version or a retained one. A demoted file's code node that depth expansion alone would reach is left out and counted in 'demoted'; a key asked for by name is always answered. A key the version doesn't hold, a session can't see or a deny glob hides is in 'missing' with one answer for every reason;
/// every key missing is still an answer. Every label is cleaned like every answer's.
/// </summary>
internal sealed class GetSubgraphService(MapReader reader, LensBasisReader bases, IScrubberFactory scrubbers) : IService<GetSubgraphRequest, SubgraphView>
{
    public async ValueTask<Result<SubgraphView>> Handle(GetSubgraphRequest request, CancellationToken cancellationToken)
    {
        var settings = SettingsReader.Read(request.Scope.Settings);
        var built = settings.Bind(s => scrubbers.For(s.ExtraPatterns));
        if (built.IsError)
        {
            return built.Error;
        }

        var read = await reader.ReadViewAtOrEmpty(request.Scope, request.Query.Read, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        var basis = await bases.For(read.Value, settings.Value, request.Scope, request.Query.Read.Draft, cancellationToken).ConfigureAwait(false);
        basis = basis with { Repos = request.Scope.Repos };
        var view = SubgraphBuilder.Build(basis, read.Value.Graph, request.Query.Keys, request.Query.Depth ?? 0, read.Value.Conflicts);
        return view.Map(v => LensTexts.Clean(v, new MapText(built.Value, FencePolicies.For(request.Scope.Caller))));
    }
}
