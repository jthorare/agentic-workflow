using System.Globalization;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Core.Overrides;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.CheckMap;

/// <summary>
/// Doctor 'context:map' (spec §8.7): warns with no map, with orphaned overrides or when the map was built under other rules than the scope's (issue 1041), fails when the store can't open.
/// How many commits the current map is behind HEAD is unknown until a build records its commit (S-4).
/// </summary>
internal sealed class CheckMapService(IGraphStore store) : IService<CheckMapRequest, MapHealth>
{
    private const string Name = "context:map";

    public async ValueTask<Result<MapHealth>> Handle(CheckMapRequest request, CancellationToken cancellationToken)
    {
        var current = await store.Current(cancellationToken).ConfigureAwait(false);
        if (current.IsError)
        {
            return Fail(current.Error);
        }

        if (current.Value is not { } version)
        {
            return new MapHealth(Name, MapHealthState.Warn, "no map yet", "sindri context map import <file>");
        }

        var graph = await store.Read(version.Number, cancellationToken).ConfigureAwait(false);
        var overrides = await store.Overrides(cancellationToken).ConfigureAwait(false);
        if (graph.IsError || overrides.IsError)
        {
            return Fail(graph.IsError ? graph.Error : overrides.Error);
        }

        // The map as status reads it: the deny globs cut first, so the two never count differently.
        var seen = request.Scope is { } scope ? MapAccess.View(graph.Value, overrides.Value, scope, pending: false, callerScope: false) : null;
        if (seen is { IsError: true })
        {
            return Fail(seen.Error);
        }

        var (shown, layer) = seen is { } ok ? (ok.Value.Graph, ok.Value.Overrides) : (graph.Value, overrides.Value);
        var detail = string.Create(CultureInfo.InvariantCulture,
            $"map v{version.Number} current ({version.Repo}, whole map: {shown.Nodes.Count} nodes, {shown.Edges.Count} edges); commits behind HEAD: unknown until a build records its commit");
        return Verdict(version, detail, OverrideApplier.Apply(shown, layer).Orphaned.Count, request.Scope);
    }

    private static MapHealth Verdict(MapVersion version, string detail, int orphaned, ContextScope? scope)
    {
        if (orphaned > 0)
        {
            return new MapHealth(Name, MapHealthState.Warn, string.Create(CultureInfo.InvariantCulture, $"{detail}; {orphaned} orphaned override(s)"),
                "sindri context map import a map that has their nodes again, or leave them for map review (S-4)");
        }

        return OlderRules(version, scope)
            ? new MapHealth(Name, MapHealthState.Warn, $"{detail}; built under older map rules", "sindri context map build (the refresh service also rebuilds it on its next tick)")
            : new MapHealth(Name, MapHealthState.Ok, detail, Fix: null);
    }

    // The map's recorded rules against the ones the scope would build under now (issue 1041); a map built before they were recorded counts as older. Without a scope there is nothing to compare.
    private static bool OlderRules(MapVersion version, ContextScope? scope) =>
        MapRuleset.Running(scope) is { } running && !string.Equals(version.Ruleset, running, StringComparison.Ordinal);

    private static MapHealth Fail(Error error) =>
        new(Name, MapHealthState.Fail, $"{error.Code}: {error.Message}", "check $AW_SINDRI_HOME/context permissions; never delete context.db");
}
