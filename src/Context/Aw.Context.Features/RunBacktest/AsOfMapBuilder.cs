using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Eval.Backtest;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Kernel;

namespace Aw.Context.Features.RunBacktest;

/// <summary>
/// A sample's map as it stood at the cut (spec §9.1, never the map_version of the day): the extractor over the cut commit's tree, the cluster source's
/// candidates with file-derived names (no labeling pass, no model call), the newest map version created at or before the issue's creation (authored
/// nodes whose code the cut doesn't have are dropped), and the override rows dated before it. The extraction is cached by repo, commit, extractor
/// version and whether clusters answered. Nothing is written to context.db.
/// </summary>
internal sealed class AsOfMapBuilder(IRepoReader repos, IClusterSource clusters, IGraphStore store, IEvalStore cache)
{
    public async ValueTask<Result<AsOfGraph>> Build(AsOfRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (repo, sample) = (request.Repo, request.Sample);
        var settings = SettingsReader.Read(request.Scope.Settings);
        if (settings.IsError)
        {
            return settings.Error;
        }

        var listed = await repos.ListFiles(repo, sample.Cut, cancellationToken).ConfigureAwait(false);
        if (listed.IsError)
        {
            return listed.Error;
        }

        var extraction = await Cut(request, listed.Value, new Rules(RecognizerSet.From(settings.Value.Recognizers), settings.Value.Graphify), cancellationToken).ConfigureAwait(false);
        if (extraction.IsError)
        {
            return extraction.Error;
        }

        var authored = await Authored(sample, new HashSet<string>(listed.Value, StringComparer.Ordinal), cancellationToken).ConfigureAwait(false);
        var overrides = await store.Overrides(cancellationToken).ConfigureAwait(false);
        if (authored.IsError || overrides.IsError)
        {
            return authored.IsError ? authored.Error : overrides.Error;
        }

        // The same cut every reader makes of the current map: the deny globs hide what they hide, and a pending proposal is no part of a map as of a day.
        var view = MapAccess.View(Join(authored.Value, extraction.Value.Map), [.. overrides.Value.Where(o => o.At < sample.CreatedAt)], request.Scope, pending: false, callerScope: false);
        return view.IsError ? view.Error : new AsOfGraph(view.Value.Applied, extraction.Value.Cached, extraction.Value.Clustered, RecognizersTimedOut: extraction.Value.TimedOut);
    }

    private async ValueTask<Result<Found>> Cut(AsOfRequest request, IReadOnlyList<string> files, Rules rules, CancellationToken cancellationToken)
    {
        var recognizers = rules.Recognizers;
        var looked = await Look(request, rules, cancellationToken).ConfigureAwait(false);
        if (looked.IsError || looked.Value.Hit is not null)
        {
            return looked.IsError ? looked.Error : looked.Value.Hit!;   // why: a non-null hit was just checked
        }

        var (key, set) = (looked.Value.Key, looked.Value.Set);
        var built = await Extract(request, recognizers, (files, set), cancellationToken).ConfigureAwait(false);
        if (built.IsError)
        {
            return built.Error;
        }

        // A map that lost a recognizer's matches to the timeout depends on the load of the day, and one whose cluster pass was partial (a truncated snapshot) is no complete map: neither is cached under the permanent key, and the next build extracts again.
        if (built.Value.TimedOut || set is { Available: true, Reason: not null })
        {
            return new Found(built.Value.Map, Cached: false, set.Available, built.Value.TimedOut);
        }

        // A cache that can't be written costs the next run its time, not this one its map; only a refused store folder (005) stops the run.
        var written = await cache.WriteMap(key, built.Value.Map, cancellationToken).ConfigureAwait(false);
        return written.IsError && string.Equals(written.Error.Code, ContextErrors.PathInWorktree.Code, StringComparison.Ordinal)
            ? written.Error
            : new Found(built.Value.Map, Cached: false, set.Available, TimedOut: false);
    }

    // A clustered extraction is cached under its own key and found without calling the cluster source; otherwise the source is asked, and the plain key is read only when it has no clusters.
    private async ValueTask<Result<Probe>> Look(AsOfRequest request, Rules rules, CancellationToken cancellationToken)
    {
        var (repo, sample) = (request.Repo, request.Sample);
        var recognizers = rules.Recognizers;
        var clustered = AsOfMapCache.WithLimits(AsOfMapCache.Key(repo.Name, sample.Cut, MapExtractor.Version, clustered: true, recognizers), rules.Limits);
        var first = await Cached(clustered, clustered: true, cancellationToken).ConfigureAwait(false);
        if (first.IsError || first.Value is not null)
        {
            return first.IsError ? first.Error : new Probe(first.Value, clustered, new ClusterSet([], Available: true));
        }

        var set = await Clusters(repo, sample.Cut, rules.Limits, cancellationToken).ConfigureAwait(false);
        var plain = AsOfMapCache.Key(repo.Name, sample.Cut, MapExtractor.Version, clustered: false, recognizers);
        var second = set.Available ? Results.Success<Found?>(value: null) : await Cached(plain, clustered: false, cancellationToken).ConfigureAwait(false);
        return second.IsError ? second.Error : new Probe(second.Value, set.Available ? clustered : plain, set);
    }

    private async ValueTask<Result<Found?>> Cached(string key, bool clustered, CancellationToken cancellationToken)
    {
        var hit = await cache.ReadMap(key, cancellationToken).ConfigureAwait(false);
        return hit.IsError ? hit.Error : Results.Success<Found?>(hit.Value is { } map ? new Found(map, Cached: true, clustered, TimedOut: false) : null);
    }

    private async ValueTask<Result<MapExtraction>> Extract(AsOfRequest request, RecognizerSet recognizers, (IReadOnlyList<string> Tree, ClusterSet Set) cut, CancellationToken cancellationToken)
    {
        var (repo, sample) = (request.Repo, request.Sample);
        var set = cut.Set;
        var files = new List<RepoFile>();
        foreach (var path in cut.Tree.Where(p => !repo.Denies(p) && (Recognizers.MayRead(p) || recognizers.Wants(p))))
        {
            var text = await repos.Read(repo, sample.Cut, path, cancellationToken).ConfigureAwait(false);
            if (text.IsError)
            {
                return text.Error;
            }

            files.Add(new RepoFile(path, text.Value));
        }

        var extraction = MapExtractor.Extract(repo.Name, files, recognizers);
        var extracted = extraction.Map;
        return new MapExtraction(extracted.Plus(ClusterFeatures.Propose(repo, set, [], extracted)), extraction.TimedOut);
    }

    // A cluster source that fails reads like one that is absent; a denied path never reaches the graph.
    private async ValueTask<ClusterSet> Clusters(ContextRepo repo, string commit, ClusterLimits limits, CancellationToken cancellationToken)
    {
        var set = await clusters.Clusters(repo, commit, limits, cancellationToken).ConfigureAwait(false);
        return set.IsError || !set.Value.Available
            ? new ClusterSet([], Available: false)
            : new ClusterSet([.. set.Value.Clusters.Select(c => c with { Files = [.. c.Files.Where(f => !repo.Denies(f))] }).Where(c => c.Files.Count > 0)], Available: true, set.Value.Reason);
    }

    /// <summary>What one as-of build reads from the profile: the extra recognizers and the cluster pass's limits.</summary>
    private sealed record Rules(RecognizerSet Recognizers, ClusterLimits Limits);

    private async ValueTask<Result<GraphSnapshot>> Authored(CorpusSample sample, HashSet<string> tree, CancellationToken cancellationToken)
    {
        var versions = await store.Versions(cancellationToken).ConfigureAwait(false);
        if (versions.IsError)
        {
            return versions.Error;
        }

        var newest = versions.Value.Where(v => v.Status is MapVersionStatus.Current or MapVersionStatus.Retained && v.Created <= sample.CreatedAt).OrderByDescending(v => v.Number).FirstOrDefault();
        if (newest is null)
        {
            return GraphSnapshot.Empty;
        }

        var graph = await store.Read(newest.Number, cancellationToken).ConfigureAwait(false);
        return graph.IsError ? graph.Error : AsOfPruning.Existing(graph.Value, tree);
    }

    // The authored layer first: what a person put there wins over what the extractor found (BuildMapService.Join).
    private static GraphSnapshot Join(GraphSnapshot authored, ExtractedMap found) =>
        authored with
        {
            Nodes = [.. authored.Nodes.Concat(found.Nodes).DistinctBy(n => n.Key.Value, StringComparer.Ordinal)],
            Edges = [.. authored.Edges.Concat(found.Edges).DistinctBy(e => (e.From.Value, e.To.Value, e.Type))],
        };

    private sealed record Probe(Found? Hit, string Key, ClusterSet Set);

    private sealed record Found(ExtractedMap Map, bool Cached, bool Clustered, bool TimedOut);
}
