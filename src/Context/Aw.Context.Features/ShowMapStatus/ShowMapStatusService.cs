using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Map;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Roles;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.ShowMapStatus;

/// <summary>The current version, its counts, its orphaned overrides and how many earlier versions are kept; a session sees its repo's scope only. With an anchor it also gives the coverage of that node's own scope, from the function the lenses use.</summary>
internal sealed class ShowMapStatusService(IGraphStore store) : IService<ShowMapStatusRequest, MapStatusView>
{
    public async ValueTask<Result<MapStatusView>> Handle(ShowMapStatusRequest request, CancellationToken cancellationToken)
    {
        if (MapAccess.Gate(request.Scope) is { } refused)
        {
            return refused;
        }

        var versions = await store.Versions(cancellationToken).ConfigureAwait(false);
        if (versions.IsError || versions.Value.FirstOrDefault(v => v.Status == MapVersionStatus.Current) is not { } version)
        {
            return versions.IsError ? versions.Error : ContextErrors.NoMap.With("there is no map yet (fix: sindri context map import <file>)");
        }

        var graph = await store.Read(version.Number, cancellationToken).ConfigureAwait(false);
        var overrides = await store.Overrides(cancellationToken).ConfigureAwait(false);
        var visible = graph.IsError ? graph.Error : overrides.IsError ? overrides.Error : MapAccess.View(graph.Value, overrides.Value, request.Scope, pending: false);
        if (visible.IsError)
        {
            return visible.Error;
        }

        var (shown, applied, layer) = (visible.Value.Graph, visible.Value.Applied, visible.Value.Overrides);
        var coverage = request.Anchor is { } name ? Coverage(applied, name, request.Scope.Repos) : Results.Success<LensCoverage?>(value: null);
        if (coverage.IsError)
        {
            return coverage.Error;
        }

        var orphaned = OverrideApplier.Apply(shown, layer).Orphaned.Count;
        return new MapStatusView(version, MapCounts.Of(shown, layer.Count), orphaned, versions.Value.Count(v => v.Status == MapVersionStatus.Retained), ScopedTo(request.Scope.Caller), coverage.Value)
        {
            RulesOlder = RulesOlder(version, request.Scope),
            Roles = Roles(shown, version, request.Scope),
        };
    }

    // Only a map that recorded its rules can be said to differ from the running ones; one built before they were recorded says nothing here (the doctor row and the refresh handle it).
    private static bool RulesOlder(MapVersion version, ContextScope scope) =>
        version.Ruleset is { } recorded && MapRuleset.Running(scope) is { } running && !string.Equals(recorded, running, StringComparison.Ordinal);

    // The role counts of the repos the caller reads, over the map as it sees it; null (left out) when no repo configures a role or has a role-like file outside every glob (issue 1105).
    private static IReadOnlyList<RepoRoles>? Roles(GraphSnapshot shown, MapVersion version, ContextScope scope)
    {
        var own = ScopedTo(scope.Caller);
        var counted = RoleCounter.Of([.. scope.Repos.Where(r => own is null || r.Name == own)], RoleCounter.CodePaths(shown, version.Repo.Value));
        return counted.Count == 0 ? null : counted;
    }

    private static ContextRepoName? ScopedTo(ContextCaller caller) => caller.Kind == ContextCallerKind.Session ? caller.Repo : null;

    // The coverage of the anchor's own scope over the map as a caller reads it (overrides applied, a pending proposal not part of the current map): the lenses' record.
    private static Result<LensCoverage?> Coverage(GraphSnapshot applied, string anchor, IReadOnlyList<ContextRepo> repos)
    {
        var node = AnchorMatch.Resolve(applied, anchor, repos);
        return node.IsError ? node.Error : Results.Success<LensCoverage?>(LensCoverageBuilder.ForAnchor(applied, node.Value.Key));
    }
}
