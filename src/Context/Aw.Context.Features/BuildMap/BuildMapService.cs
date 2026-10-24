using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Locks;
using Aw.Context.Core.Map;
using Aw.Context.Core.Overrides;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.BuildMap;

/// <summary>
/// Proposes the map from the repos (spec Phase 4): under the heavy-job lock, reads each mapped repo at its HEAD (never the
/// working tree, never a denied path), extracts the deterministic anchors with file-level 'implements', adds graphify
/// clusters as candidate features and, only when 'context.labeling.enabled', names them with a model. The result joins the
/// current map (a draft is a whole map, so a promote loses nothing) and is written as a draft in one step; 'current' is never
/// touched, and every override, pending 'propose' rows included, is re-applied on read. A missing graphify is
/// 'degraded: clusters', not an error.
/// </summary>
internal sealed class BuildMapService(
    IRepoReader repos,
    IClusterSource clusterSource,
    ILabeler labeler,
    IHeavyJobLock heavy,
    IDefaultBranch defaults,
    IGraphStore store,
    TimeProvider clock) : IService<BuildMapRequest, MapBuildReport>
{
    private const int ProgressEvery = 50;

    public async ValueTask<Result<MapBuildReport>> Handle(BuildMapRequest request, CancellationToken cancellationToken)
    {
        var scope = request.Scope;
        var mapped = SettingsReader.Read(scope.Settings).Bind(settings => ScopeRules.Resolve(settings, scope.Repos, scope.Caller).Map(r => (Settings: settings, Repos: r)));
        if (mapped.IsError)
        {
            return mapped.Error;
        }

        var (settings, mappedRepos) = mapped.Value;
        if (mappedRepos.Count == 0)
        {
            return ContextErrors.SettingsInvalid.With("context.repos: the profile maps no repo to build from (fix: add a repo to the profile and approve it)");
        }

        var slot = await heavy.Take(request.Progress, cancellationToken).ConfigureAwait(false);
        if (slot.IsError)
        {
            return slot.Error;
        }

        await using (slot.Value.ConfigureAwait(false))
        {
            return await Build(request.Progress, settings, mappedRepos, scope.Repos, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask<Result<MapBuildReport>> Build(IProgress<string>? progress, ContextSettings settings, IReadOnlyList<ContextRepo> mappedRepos, IReadOnlyList<ContextRepo> profileRepos, CancellationToken cancellationToken)
    {
        var read = await ReadRepos(progress, settings, mappedRepos, cancellationToken).ConfigureAwait(false);
        return read.IsError ? read.Error : await Save(read.Value, profileRepos, mappedRepos, cancellationToken).ConfigureAwait(false);
    }

    // What the build read: the merged map, and where each repo stood as this build read it (the pre-build 'map heads' read can differ when trunk moved in between), kept with the draft for the publish rules.
    private async ValueTask<Result<Reading>> ReadRepos(IProgress<string>? progress, ContextSettings settings, IReadOnlyList<ContextRepo> mappedRepos, CancellationToken cancellationToken)
    {
        var found = ExtractedMap.Empty;
        var degraded = new List<string>();
        (string Commit, ContextRepoName Name)? primary = null;
        var heads = new List<MapHead>(mappedRepos.Count);
        foreach (var repo in mappedRepos)
        {
            var one = await Extract(progress, settings, repo, cancellationToken).ConfigureAwait(false);
            if (one.IsError)
            {
                return one.Error;
            }

            found = found.Plus(one.Value.Map);
            degraded.AddRange(one.Value.Degraded.Except(degraded, StringComparer.Ordinal));
            primary ??= (one.Value.Commit, repo.Name);
            heads.Add(new MapHead(repo.Name, one.Value.Commit, await OnDefault(repo, one.Value.Commit, cancellationToken).ConfigureAwait(false)));
        }

        // why: mappedRepos is not empty, so the loop set the primary
        return MapHead.Check(heads) is { } invalid ? invalid : new Reading(found, degraded, primary!.Value.Name, primary.Value.Commit, heads);
    }

    private async ValueTask<Result<MapBuildReport>> Save(Reading read, IReadOnlyList<ContextRepo> profileRepos, IReadOnlyList<ContextRepo> mappedRepos, CancellationToken cancellationToken)
    {
        var current = await store.Snapshot(draft: false, cancellationToken).ConfigureAwait(false);
        if (current.IsError)
        {
            return current.Error;
        }

        var basis = current.Value?.Graph ?? GraphSnapshot.Empty;
        var open = await store.Snapshot(draft: true, cancellationToken).ConfigureAwait(false);
        if (open.IsError)
        {
            return open.Error;
        }

        var at = clock.GetUtcNow();
        var (kept, undo) = MapAccess.WithoutDenied(basis, current.Value?.Overrides ?? [], profileRepos, mappedRepos, at);
        undo = UndoAlso(undo, open.Value, profileRepos, mappedRepos, at);
        var draft = await store.SaveDraft(new MapImport(read.Name, read.Commit, Join(kept, read.Found), undo, read.Heads), at, cancellationToken).ConfigureAwait(false);
        return draft.IsError ? draft.Error : new MapBuildReport(draft.Value, Proposed(kept, read.Found), read.Degraded, read.Heads);
    }

    private sealed record Reading(ExtractedMap Found, IReadOnlyList<string> Degraded, ContextRepoName Name, string Commit, IReadOnlyList<MapHead> Heads);

    // Whether the default branch holds the commit the build read; a checkout whose default ref can't be read counts as no (the repo is then not waited on), never a failed build.
    private async ValueTask<bool> OnDefault(ContextRepo repo, string commit, CancellationToken cancellationToken)
    {
        var reached = await defaults.Reaches(repo, commit, cancellationToken).ConfigureAwait(false);
        return reached.IsSuccess && reached.Value;
    }

    // The open draft this build replaces may hold nodes the current map doesn't (a surface found since the last promote): the decisions about those it would lose to the cut are undone too, in the same write.
    private static IReadOnlyList<MapOverride> UndoAlso(IReadOnlyList<MapOverride> undo, MapSnapshot? openDraft, IReadOnlyList<ContextRepo> profileRepos, IReadOnlyList<ContextRepo> mappedRepos, DateTimeOffset at) =>
        openDraft is null
            ? undo
            : [.. undo.Concat(MapAccess.WithoutDenied(openDraft.Graph, openDraft.Overrides, profileRepos, mappedRepos, at).Undo).DistinctBy(o => o.Undoes?.Value, StringComparer.Ordinal)];

    private async ValueTask<Result<RepoExtraction>> Extract(IProgress<string>? progress, ContextSettings settings, ContextRepo repo, CancellationToken cancellationToken)
    {
        var recognizers = RecognizerSet.From(settings.Recognizers);
        var read = await ReadFiles(progress, repo, recognizers, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        var (commit, files) = read.Value;
        var (extracted, timedOut) = MapExtractor.Extract(repo.Name, files, recognizers);
        var clusters = await Clusters(repo, commit, settings.Graphify, cancellationToken).ConfigureAwait(false);
        var labels = await Labels(progress, settings, clusters, cancellationToken).ConfigureAwait(false);
        if (labels.IsError && labels.Error.Code == ContextErrors.LabelingUnavailable.Code)
        {
            return labels.Error;
        }

        // A model that couldn't answer leaves the clusters their file-derived names.
        var features = ClusterFeatures.Propose(repo.Name, clusters, labels.IsError ? [] : labels.Value.Labels, extracted);
        return new RepoExtraction(extracted.Plus(features), commit, Degraded(clusters, labels, timedOut));
    }

    private static string[] Degraded(ClusterSet clusters, Result<LabelPass> labels, bool timedOut) =>
        [.. clusters is { Available: true, Reason: null } ? [] : new[] { clusters.Reason is { } why ? $"clusters: {why}" : "clusters" }, .. labels.IsError || !labels.Value.Complete ? new[] { "labels" } : [], .. timedOut ? new[] { "recognizers" } : []];

    // A denied path is never read, whatever a recognizer wants.
    private static bool Readable(ContextRepo repo, RecognizerSet recognizers, string path) => !repo.Denies(path) && (Recognizers.MayRead(path) || recognizers.Wants(path));

    // HEAD's tree minus the denied paths, and the text of each file the extractor may read.
    private async ValueTask<Result<(string Commit, List<RepoFile> Files)>> ReadFiles(IProgress<string>? progress, ContextRepo repo, RecognizerSet recognizers, CancellationToken cancellationToken)
    {
        var head = await repos.Head(repo, cancellationToken).ConfigureAwait(false);
        var listed = head.IsError ? head.Error : await repos.ListFiles(repo, head.Value, cancellationToken).ConfigureAwait(false);
        if (head.IsError || listed.IsError)
        {
            return head.IsError ? head.Error : listed.Error;
        }

        var wanted = listed.Value.Where(p => Readable(repo, recognizers, p)).ToList();
        var files = new List<RepoFile>(wanted.Count);
        foreach (var path in wanted)
        {
            var text = await repos.Read(repo, head.Value, path, cancellationToken).ConfigureAwait(false);
            if (text.IsError)
            {
                return text.Error;
            }

            files.Add(new RepoFile(path, text.Value));
            if (files.Count % ProgressEvery == 0 && files.Count < wanted.Count)
            {
                Tell(progress, files.Count, wanted.Count);
            }
        }

        Tell(progress, files.Count, wanted.Count);
        return (head.Value, files);
    }

    // A cluster source that fails reads like one that is absent; a denied path never reaches the labeler or the graph.
    private async ValueTask<ClusterSet> Clusters(ContextRepo repo, string commit, ClusterLimits limits, CancellationToken cancellationToken)
    {
        var set = await clusterSource.Clusters(repo, commit, limits, cancellationToken).ConfigureAwait(false);
        if (set.IsError || !set.Value.Available)
        {
            return new ClusterSet([], Available: false, set.IsError ? null : set.Value.Reason);
        }

        return new ClusterSet(
            [.. set.Value.Clusters.Select(c => c with { Files = [.. c.Files.Where(f => !repo.Denies(f))] }).Where(c => c.Files.Count > 0)],
            Available: true,
            set.Value.Reason);
    }

    private async ValueTask<Result<LabelPass>> Labels(IProgress<string>? progress, ContextSettings settings, ClusterSet clusters, CancellationToken cancellationToken)
    {
        if (!settings.Labeling.Enabled || clusters.Clusters.Count == 0)
        {
            return Results.Success(new LabelPass([]));
        }

        progress?.Report(string.Create(CultureInfo.InvariantCulture, $"labeling {clusters.Clusters.Count} clusters"));
        return await labeler.Label(clusters.Clusters, settings.ExtraPatterns, settings.Labeling.MaxTokens, cancellationToken).ConfigureAwait(false);
    }

    private static void Tell(IProgress<string>? progress, int done, int total) =>
        progress?.Report(string.Create(CultureInfo.InvariantCulture, $"extracting {done}/{total} files"));

    // The current map first (already without what the built repos' deny globs take out, spec §6.2): what a person put there wins over what the build found, and a promote loses nothing.
    private static GraphSnapshot Join(GraphSnapshot kept, ExtractedMap found) =>
        kept with
        {
            Nodes = [.. kept.Nodes.Concat(found.Nodes).DistinctBy(n => n.Key.Value, StringComparer.Ordinal)],
            Edges = [.. kept.Edges.Concat(found.Edges).DistinctBy(e => (e.From.Value, e.To.Value, e.Type))],
        };

    private static List<ProposedNode> Proposed(GraphSnapshot current, ExtractedMap found) =>
    [
        .. found.Nodes.Where(n => n.Key.Kind != NodeKind.Code && current.Resolve(n.Key) is null).OrderBy(n => n.Key.Value, StringComparer.Ordinal)
            .Select(n => new ProposedNode(n.Key, n.Provenance, n.Confidence,
                [.. found.Edges.Where(e => e.Type == EdgeType.Implements && e.To.Value == n.Key.Value && e.From.Kind == NodeKind.Code).Select(e => e.From).DistinctBy(k => k.Value, StringComparer.Ordinal).OrderBy(k => k.Value, StringComparer.Ordinal)])),
    ];

    private sealed record RepoExtraction(ExtractedMap Map, string Commit, IReadOnlyList<string> Degraded);
}
