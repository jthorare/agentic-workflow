using Aw.Context.Core.Engine;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.LensSupport;
using Aw.Context.Core.Map;
using Aw.Context.Core.Trust;
using Aw.Context.Core.Walks;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.GetLenses;

/// <summary>
/// The union of the bands around several nodes (scoping spec C1, schema 2): one snapshot of the map the caller may read serves every anchor. An anchor the version doesn't hold, a session can't
/// see or a deny glob hides is listed in 'missingAnchors' with one answer for every reason; none resolving is SND-CONTEXT-003 and an ambiguous name SND-CONTEXT-037.
/// </summary>
internal sealed class GetLensesSetService(MapReader reader, LensBasisReader bases, IScrubberFactory scrubbers, WalkOptions? walk = null) : IService<GetLensesSetRequest, LensesSetView>
{
    public async ValueTask<Result<LensesSetView>> Handle(GetLensesSetRequest request, CancellationToken cancellationToken)
    {
        var settings = SettingsReader.Read(request.Scope.Settings);
        var built = settings.Bind(s => scrubbers.For(s.ExtraPatterns));
        if (built.IsError)
        {
            return built.Error;
        }

        var depth = request.Depth ?? settings.Value.CascadeDepth;
        var anchors = request.Anchors.Distinct(StringComparer.Ordinal).ToList();
        if ((QueryRules.CheckAnchors(anchors, "lenses") ?? QueryRules.CheckDepth(depth)) is { } refused)
        {
            return refused;
        }

        var hops = QueryRules.ParseHops(request.Hops);
        if (hops.IsError)
        {
            return hops.Error;
        }

        var read = await reader.ReadViewAt(request.Scope, request.Read, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        var basis = (await bases.For(read.Value, settings.Value, request.Scope, request.Read.Draft, cancellationToken).ConfigureAwait(false)) with { Cascade = QueryRules.CascadeFor(hops.Value, settings.Value), Walk = walk ?? WalkOptions.Serial };
        using var band = FillBands.Start(FillBands.Lenses);
        var view = LensesBuilder.BuildSet(basis, read.Value.Graph, anchors, depth, read.Value.Conflicts);
        return view.Map(v => LensTexts.Clean(DemotedFiles.Last(v, request.Scope.Repos), new MapText(built.Value, FencePolicies.For(request.Scope.Caller))));
    }
}
