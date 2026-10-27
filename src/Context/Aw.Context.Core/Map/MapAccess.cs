using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Overrides;
using Aw.Kernel;

namespace Aw.Context.Core.Map;

/// <summary>
/// Who may read which part of the map (spec §3): humans and services see all of it; a session only while
/// 'context.sessions.enabled' is on (SND-CONTEXT-020), for a repo the profile maps, and only the nodes that repo implements and the records and decisions linked to them (owner decision 2026-10-15, B),
/// with what lies between them. A repo that implements nothing answers SND-CONTEXT-003, like any unknown anchor.
/// </summary>
public static class MapAccess
{
    /// <summary>
    /// The one function from a stored snapshot to what a caller may see (review #220 run 4), which every reader and every planner uses, in one order: (1) what the repos'
    /// deny globs hide is decided from the stored nodes and the nodes the overrides add (<see cref="DenyCut"/>: the code nodes of denied paths, the record nodes of denied documents
    /// and the decisions quoting them, the nodes only a denied file evidences, the features only those files implement, their edges and denied evidence), whatever a person's
    /// reject, delete, merge or accept made of them; (2) the stored graph is cut by that decision; (3) the overrides are cut by it (the ones about a hidden node, a proposal without
    /// its allowed evidence, and a reject that names its proposal by either evidence); (4) the cut overrides are applied to the cut graph (a pending proposal only when
    /// <paramref name="pending"/>), which is <see cref="MapView.Applied"/>; then, for a session, only its repo's scope. Lenses, status, export, the review queue, promote
    /// and the history can't disagree.
    /// </summary>
    /// <param name="graph">The stored graph.</param>
    /// <param name="overrides">Every override.</param>
    /// <param name="scope">The profile's context section and repos, and the caller (run <see cref="Gate"/> first).</param>
    /// <param name="pending">True when a 'propose' row's node is part of the map (a draft); a current map never holds a pending proposal.</param>
    /// <param name="callerScope">False to apply the deny globs only, for a decision the engine doesn't scope to the caller's repo (a review, a proposal's status).</param>
    /// <returns>The visible view, or SND-CONTEXT-001 for an invalid section, or SND-CONTEXT-003 when a session's repo implements nothing in the map.</returns>
    public static Result<MapView> View(GraphSnapshot graph, IReadOnlyList<MapOverride> overrides, ContextScope scope, bool pending, bool callerScope = true) =>
        Merged(graph, overrides, scope, (pending, callerScope, EmptyIsError: true), intent: null);

    /// <summary>The view with the synced intent layer merged into the graph before the cut (spec §3 Repo scope, §6.2): its records, decisions and links meet the deny globs and a session's scope like any node.</summary>
    /// <param name="intent">The synced layer.</param>
    /// <param name="graph">The stored graph of the version.</param>
    /// <param name="overrides">The version's overrides.</param>
    /// <param name="scope">The profile's context section and repos, and the caller.</param>
    /// <param name="pending">True when a 'propose' row's node is part of the map (a draft).</param>
    /// <returns>The visible view, or SND-CONTEXT-001/003.</returns>
    public static Result<MapView> ViewWithIntent(IntentLayer intent, GraphSnapshot graph, IReadOnlyList<MapOverride> overrides, ContextScope scope, bool pending) =>
        Merged(graph, overrides, scope, (pending, CallerScope: true, EmptyIsError: true), intent);

    /// <summary>
    /// <see cref="ViewWithIntent"/> for a read whose question names its nodes (the subgraph): a session whose repo implements nothing sees an empty map, so every key it asks for is
    /// simply missing, the same answer as for a key the map doesn't hold, instead of SND-CONTEXT-003.
    /// </summary>
    /// <param name="intent">The synced layer.</param>
    /// <param name="graph">The stored graph of the version.</param>
    /// <param name="overrides">The version's overrides.</param>
    /// <param name="scope">The profile's context section and repos, and the caller.</param>
    /// <param name="pending">True when a 'propose' row's node is part of the map (a draft).</param>
    /// <returns>The visible view (empty for such a session), or SND-CONTEXT-001.</returns>
    public static Result<MapView> ViewWithIntentOrEmpty(IntentLayer intent, GraphSnapshot graph, IReadOnlyList<MapOverride> overrides, ContextScope scope, bool pending) =>
        Merged(graph, overrides, scope, (pending, CallerScope: true, EmptyIsError: false), intent);

    private static Result<MapView> Merged(GraphSnapshot graph, IReadOnlyList<MapOverride> overrides, ContextScope scope, (bool Pending, bool CallerScope, bool EmptyIsError) mode, IntentLayer? intent)
    {
        var (pending, callerScope, emptyIsError) = mode;
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(overrides);
        ArgumentNullException.ThrowIfNull(scope);
        var settings = SettingsReader.Read(scope.Settings);
        if (settings.IsError)
        {
            return settings.Error;
        }

        var repos = ScopeRules.Resolve(settings.Value, scope.Repos, scope.Caller);
        if (repos.IsError)
        {
            return repos.Error;
        }

        // The synced intent layer joins the stored graph before the cut, so the deny globs and a session's scope judge its records, decisions and links like any node.
        var merged = intent is null ? graph : IntentOverlay.Apply(graph, intent).Graph;

        // Whoever asks, every profile repo's deny globs judge record keys and evidence refs (they name their repo), not just the ones context.repos maps; a code: key names no repo, so only the mapped repos' globs judge it: the snapshot is global, so a session in repo A must not reach a document repo B denies (review #234 runs 1 and 2).
        // The resolved repos are for the gate and the session's scope step only.
        var cut = DenyCut.Of(merged, overrides, scope.Repos, ScopeRules.Mapped(settings.Value, scope.Repos));   // every override, pending or not: what is hidden doesn't depend on which reader asks
        var (cutGraph, cutOverrides) = (cut.Apply(merged), cut.Apply(overrides));
        var applied = OverrideApplier.Apply(cutGraph, pending ? cutOverrides : [.. cutOverrides.Where(o => o.Action != OverrideAction.Propose)]).Graph;
        var view = new MapView(cutGraph, applied, cutOverrides, cut);
        return !callerScope || scope.Caller.Kind != ContextCallerKind.Session ? view : ForSession(view, scope.Caller, emptyIsError);
    }

    private static Result<MapView> ForSession(MapView view, ContextCaller caller, bool emptyIsError)
    {
        var scopedApplied = caller.Repo is { } repo ? view.Applied.ScopedTo(repo) : GraphSnapshot.Empty;
        if (scopedApplied.Nodes.Count == 0 && emptyIsError)
        {
            return ContextErrors.UnknownAnchor.With("no map nodes are implemented in this session's repo (X-Sindri-Repo)");
        }

        // Both scopings run the same repo scope over graphs that differ only by the overrides applied; status counts come from scopedGraph and reads from scopedApplied.
        var scopedGraph = caller.Repo is { } own ? view.Graph.ScopedTo(own) : GraphSnapshot.Empty;
        return view.Scoped(scopedGraph, scopedApplied, [.. view.Overrides.Where(o => scopedGraph.Resolve(o.Target) is not null)]);
    }

    /// <summary>The checks that need no map, the same ones every engine read makes: the context section is valid (001), and a session needs sessions on (020) and a repo the profile maps (003).</summary>
    /// <param name="scope">The profile's context section and repos, and the caller.</param>
    /// <returns>Null when the caller may read, otherwise the error.</returns>
    public static Error? Gate(ContextScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var settings = SettingsReader.Read(scope.Settings);
        if (settings.IsError)
        {
            return settings.Error;
        }

        var repos = ScopeRules.Resolve(settings.Value, scope.Repos, scope.Caller);
        return repos.IsError ? repos.Error : null;
    }

    /// <summary>
    /// Whether the profile maps the repo a map file describes (an import's check): a map for a repo the profile doesn't
    /// map would answer every session read with SND-CONTEXT-003.
    /// </summary>
    /// <param name="repo">The file's 'repo'.</param>
    /// <param name="scope">The profile's context section and repos, and the caller.</param>
    /// <returns>Null when mapped, otherwise SND-CONTEXT-001 (an invalid section) or SND-CONTEXT-031 on 'repo'.</returns>
    public static Error? Maps(ContextRepoName repo, ContextScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var settings = SettingsReader.Read(scope.Settings);
        if (settings.IsError)
        {
            return settings.Error;
        }

        var mapped = ScopeRules.Resolve(settings.Value, scope.Repos, scope.Caller with { Kind = ContextCallerKind.Human });
        return mapped.IsError || mapped.Value.Any(r => string.Equals(r.Name.Value, repo.Value, StringComparison.Ordinal))
            ? (mapped.IsError ? mapped.Error : null)
            : ContextErrors.MapFileInvalid.With(
                $"repo: '{repo.Value}' isn't a repo the approved profile maps ({string.Join(", ", mapped.Value.Select(r => r.Name.Value))}); nothing was written (fix: name one of them, or add the repo to the profile and approve it)");
    }

    /// <summary>
    /// What a build keeps of the current map under the profile's deny globs (<c xml:lang="en">index.denyPaths</c>): <see cref="View"/>'s deny cut (steps 1 and 2) over the current graph, judged with the layer's
    /// overrides as every reader judges it, and the undo rows that take a person's accepts, renames, moves and merges about what the cut removes out with it (the build writes them with the
    /// draft, in one step, so the draft has no orphan the globs made).
    /// </summary>
    /// <param name="current">The current graph.</param>
    /// <param name="overrides">Every override.</param>
    /// <param name="profile">Every profile repo: their deny globs judge record keys and evidence refs.</param>
    /// <param name="mapped">The repos the map is built from: only their deny globs judge a <c xml:lang="en">code:</c> key.</param>
    /// <param name="at">When the build runs.</param>
    /// <returns>The graph itself and no rows when nothing is denied.</returns>
    public static (GraphSnapshot Graph, IReadOnlyList<MapOverride> Undo) WithoutDenied(GraphSnapshot current, IReadOnlyList<MapOverride> overrides, IReadOnlyList<ContextRepo> profile, IReadOnlyList<ContextRepo> mapped, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(overrides);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(mapped);
        var cut = DenyCut.Of(current, overrides, profile, mapped);
        return (cut.Apply(current), cut.UndoRows(overrides, at));
    }

    /// <summary>What a build keeps of a graph under the mapped repos' demote globs (#1043): the 'implements' edges of demoted files are dropped, and the accepts standing on them are undone (the build writes the undo rows with the draft).</summary>
    /// <param name="graph">The graph a build keeps.</param>
    /// <param name="overrides">Every override of that graph's map.</param>
    /// <param name="mapped">The repos the map is built from: only their demote globs judge a <c xml:lang="en">code:</c> key.</param>
    /// <param name="at">When the build runs.</param>
    /// <returns>The graph itself and no rows when nothing is demoted.</returns>
    public static (GraphSnapshot Graph, IReadOnlyList<MapOverride> Undo) WithoutDemoted(GraphSnapshot graph, IReadOnlyList<MapOverride> overrides, IReadOnlyList<ContextRepo> mapped, DateTimeOffset at) =>
        DemoteCut.Apply(graph, overrides, mapped, at);

    /// <summary>What a build keeps of a graph when the map rules changed (#1043): an edge only the engine's accept holds up, which the new build no longer finds, is dropped and its accepts are undone (the build writes the undo rows with the draft).</summary>
    /// <param name="graph">The graph a build keeps.</param>
    /// <param name="overrides">Every override of that graph's map.</param>
    /// <param name="found">What this build found.</param>
    /// <param name="rulesChanged">Whether the build's rules differ from the ones the map was built under.</param>
    /// <param name="at">When the build runs.</param>
    /// <returns>The graph itself and no rows when the rules did not change or nothing is lost.</returns>
    public static (GraphSnapshot Graph, IReadOnlyList<MapOverride> Undo) WithoutLostEngineAccepts(GraphSnapshot graph, IReadOnlyList<MapOverride> overrides, ExtractedMap found, bool rulesChanged, DateTimeOffset at) =>
        RulesRedecide.Apply(graph, overrides, found, rulesChanged, at);

    /// <summary>The export document for a version's visible graph: decision nodes and 'quotes' edges are left to the decisions, which imply them.</summary>
    /// <param name="version">The version.</param>
    /// <param name="graph">The visible graph.</param>
    /// <param name="overrides">The visible overrides.</param>
    /// <param name="exportedAt">The time now.</param>
    /// <returns>The document, with orphaned overrides listed.</returns>
    public static MapDocument Export(MapVersion version, GraphSnapshot graph, IReadOnlyList<MapOverride> overrides, DateTimeOffset exportedAt)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(graph);
        var orphaned = OverrideApplier.Apply(graph, overrides).Orphaned;
        var o = StringComparer.Ordinal;
        return new MapDocument(
            MapDocumentValidator.SchemaVersions[^1], MapDocumentKind.Export, version.Repo,
            [.. graph.Nodes.Where(n => n.Key.Kind != NodeKind.Decision).OrderBy(n => n.Key.Value, o)],
            [.. graph.Edges.Where(e => e.Type != EdgeType.Quotes).OrderBy(e => e.From.Value, o).ThenBy(e => e.To.Value, o).ThenBy(e => e.Type).ThenBy(e => e.Order)],
            [.. graph.Aliases.OrderBy(a => a.Alias.Value, o)],
            [.. graph.Decisions.OrderBy(d => d.ToKey().Value, o)],
            [.. overrides.OrderBy(x => x.At).ThenBy(x => x.ToId().Value, o)],
            version.KeyVersion, version.Commit, version.Number, exportedAt,
            [.. orphaned.Select(x => x.Target).DistinctBy(k => k.Value, o).OrderBy(k => k.Value, o)]);
    }
}
