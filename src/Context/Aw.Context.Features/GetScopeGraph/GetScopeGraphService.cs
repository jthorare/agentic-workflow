using Aw.Context.Core.Engine;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.LensSupport;
using Aw.Context.Core.Map;
using Aw.Context.Core.ScopeGraph;
using Aw.Context.Core.Trust;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.GetScopeGraph;

/// <summary>
/// The scoping graph (issue 1005): the lenses over the anchors, read from one snapshot of the map the caller may see, cut to a ranked, capped node set with the edges among it.
/// The anchors, depth and hops are the lenses' own (GetLensesSetService), so the graph is the lenses without their bands.
/// </summary>
internal sealed class GetScopeGraphService(MapReader reader, LensBasisReader bases, IScrubberFactory scrubbers) : IService<GetScopeGraphRequest, ScopeGraphView>
{
    public async ValueTask<Result<ScopeGraphView>> Handle(GetScopeGraphRequest request, CancellationToken cancellationToken)
    {
        var query = request.Query;
        var settings = SettingsReader.Read(request.Scope.Settings);
        var built = settings.Bind(s => scrubbers.For(s.ExtraPatterns));
        if (built.IsError)
        {
            return built.Error;
        }

        var depth = query.Range.Depth ?? settings.Value.CascadeDepth;
        var anchors = query.Anchors.Distinct(StringComparer.Ordinal).ToList();
        if ((QueryRules.CheckAnchors(anchors, "scoping graph") ?? QueryRules.CheckDepth(depth)) is { } refused)
        {
            return refused;
        }

        var hops = QueryRules.ParseHops(query.Range.Hops);
        if (hops.IsError)
        {
            return hops.Error;
        }

        var read = await reader.ReadViewAt(request.Scope, query.Read, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        var cascade = QueryRules.CascadeFor(hops.Value, settings.Value);
        var basis = (await bases.For(read.Value, settings.Value, request.Scope, query.Read.Draft, cancellationToken).ConfigureAwait(false)) with { Cascade = cascade };
        var lenses = LensesBuilder.BuildSet(basis, read.Value.Graph, anchors, depth, read.Value.Conflicts);
        var text = new MapText(built.Value, FencePolicies.For(request.Scope.Caller));
        return lenses.Map(v => ScopeGraphBuilder.Build(query, LensTexts.Clean(v, text), read.Value.Graph, cascade.Hops, text));
    }
}
