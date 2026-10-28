using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Results;
using Aw.Context.Core.Roles;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.BuildMap;

/// <summary>
/// Proposes the map from the repos (spec Phase 4): one build at a time (<see cref="MapBuildGate"/>), reads each mapped repo at its HEAD (never the
/// working tree, never a denied path), extracts the deterministic anchors with file-level 'implements', adds graphify
/// clusters as candidate features and, only when 'context.labeling.enabled', names them with a model. The result joins the
/// current map (a draft is a whole map, so a promote loses nothing) and is written as a draft in one step; 'current' is never
/// touched, and every override, pending 'propose' rows included, is re-applied on read. A missing graphify is
/// 'degraded: clusters', not an error.
/// </summary>
internal sealed partial class BuildMapService(
    MapInputs inputs,
    IClusterSource clusterSource,
    MapNamers namers,
    MapBuildGate gate,
    IDefaultBranch defaults,
    IGraphStore store,
    TimeProvider clock) : IService<BuildMapRequest, MapBuildReport>
{
    private const int ProgressEvery = 50;

    public async ValueTask<Result<MapBuildReport>> Handle(BuildMapRequest request, CancellationToken cancellationToken)
    {
        var scope = request.Scope;
        var mapped = Mapped(scope);
        if (mapped.IsError)
        {
            return mapped.Error;
        }

        var (settings, mappedRepos) = mapped.Value;
        using var turn = await gate.Enter(cancellationToken).ConfigureAwait(false);
        return await Build(request.Progress, settings, mappedRepos, scope.Repos, cancellationToken).ConfigureAwait(false);
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
        var pins = mappedRepos.SelectMany(r => r.Concepts).ToList();
        var degraded = new List<string>();
        (string Commit, ContextRepoName Name)? primary = null;
        var heads = new List<MapHead>(mappedRepos.Count);
        var dropped = new List<RoleDropped>();
        var extractions = new List<(ContextRepo Repo, RepoExtraction Read)>(mappedRepos.Count);
        foreach (var repo in mappedRepos)
        {
            var one = await Extract(progress, settings, repo, pins, cancellationToken).ConfigureAwait(false);
            if (one.IsError)
            {
                return one.Error;
            }

            found = found.Plus(one.Value.Map);
            degraded.AddRange(one.Value.Degraded.Except(degraded, StringComparer.Ordinal));
            dropped.AddRange(one.Value.Dropped);
            extractions.Add((repo, one.Value));
            primary ??= (one.Value.Commit, repo.Name);
            heads.Add(new MapHead(repo.Name, one.Value.Commit, await OnDefault(repo, one.Value.Commit, cancellationToken).ConfigureAwait(false)));
        }

        found = WithStructure(found);
        // The records come after the homes are assigned: a record over the file cap rolls up to the features its files are homed in.
        var (records, recordDegraded, recordNotes) = await Records(settings, extractions, found, cancellationToken).ConfigureAwait(false);
        found = found.Plus(records);
        degraded.AddRange(recordDegraded);
        // why: mappedRepos is not empty, so the loop set the primary
        return MapHead.Check(heads) is { } invalid ? invalid : new Reading(found, degraded, primary!.Value.Name, primary.Value.Commit, heads, settings) { Dropped = dropped, RecordNotes = recordNotes };
    }

    // THE SEAM for the structure steps that need every repo's files at once (map structure spec §3): S4's FeatureRollup.Apply goes here, over the merged extraction (S3's
    // HomeFeatures.Assign runs per repo inside Extract). A build and 'context map checks --build' both read through ReadRepos, so the checks measure what the build would save.
    private static ExtractedMap WithStructure(ExtractedMap found) =>
        // why: an import can cross repos only when its target file is in the map, so the file edges roll up on the joined map, after every repo has contributed its homes and edges
        FeatureRollup.Apply(found);

    private async ValueTask<Result<MapBuildReport>> Save(Reading read, IReadOnlyList<ContextRepo> profileRepos, IReadOnlyList<ContextRepo> mappedRepos, CancellationToken cancellationToken)
    {
        var cut = await CutBasis(read, profileRepos, mappedRepos, cancellationToken).ConfigureAwait(false);
        if (cut.IsError)
        {
            return cut.Error;
        }

        var (kept, undo, ruleset, at, replaced) = cut.Value;
        // The concepts ('index.concepts' pins of the mapped repos first) are derived over the whole joined graph (records and decisions included) and saved in the draft, so reads never call a model.
        // A namer or a code index that can't answer leaves the deterministic names or the plain grouping and says so as a degraded note.
        // The structure checks (map structure spec §4) run on the joined graph before the concept pass and before any write: a refused build pays for neither and leaves the last draft and the current map as they were.
        var joined = Join(WithoutDerivedConcepts(kept), read.Found);
        var measured = MapChecks.Measure(joined, read.Settings.CheckLimits);
        var checks = measured.Decide(read.Settings.Checks);
        if (checks.Refusal is { } refused)
        {
            return refused;
        }

        var concepts = await namers.Concepts.Run(read.Settings, joined, [.. mappedRepos.SelectMany(r => r.Concepts)], mappedRepos, cancellationToken).ConfigureAwait(false);
        var draft = await store.SaveDraft(new MapImport(read.Name, read.Commit, concepts.Graph, undo, read.Heads) { Ruleset = ruleset, Dropped = read.Dropped, Checks = checks.Stored }, at, cancellationToken).ConfigureAwait(false);
        return draft.IsError ? draft.Error : new MapBuildReport(draft.Value, Proposed(kept, read.Found, mappedRepos.Select(r => r.Name.Value).ToList()), [.. read.Degraded, .. checks.Notes, .. concepts.Notes], read.Heads) { Structure = replaced, Checks = measured, Notes = [.. concepts.ReportNotes, .. read.RecordNotes] };
    }

    private sealed record Reading(ExtractedMap Found, IReadOnlyList<string> Degraded, ContextRepoName Name, string Commit, IReadOnlyList<MapHead> Heads, ContextSettings Settings)
    {
        public IReadOnlyList<RoleDropped> Dropped { get; init; } = [];

        public IReadOnlyList<string> RecordNotes { get; init; } = [];
    }

    // Whether the default branch holds the commit the build read; a checkout whose default ref can't be read counts as no (the repo is then not waited on), never a failed build.
    private async ValueTask<bool> OnDefault(ContextRepo repo, string commit, CancellationToken cancellationToken)
    {
        var reached = await defaults.Reaches(repo, commit, cancellationToken).ConfigureAwait(false);
        return reached.IsSuccess && reached.Value;
    }

    // The open draft this build replaces may hold nodes the current map doesn't (a surface found since the last promote): the decisions about those it would lose to the cut are undone too, in the same write.
    private static IReadOnlyList<MapOverride> UndoAlso(IReadOnlyList<MapOverride> undo, MapSnapshot? openDraft, BuildCuts cuts) =>
        openDraft is null
            ? undo
            : [.. undo.Concat(cuts.Apply(openDraft.Graph, openDraft.Overrides).Undo).DistinctBy(o => o.Undoes?.Value ?? o.ToId().Value, StringComparer.Ordinal)];

    private async ValueTask<Result<RepoExtraction>> Extract(IProgress<string>? progress, ContextSettings settings, ContextRepo repo, IReadOnlyList<ConceptPin> pins, CancellationToken cancellationToken)
    {
        var recognizers = RecognizerSet.From(settings.Recognizers);
        var read = await ReadFiles(progress, repo, (recognizers, settings.SchemaGlobs), cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        var (commit, files, paths) = read.Value;
        var (extracted, timedOut) = MapExtractor.Extract(repo.Name, files, recognizers, settings.SchemaGlobs);
        var home = HomeFeatures.Assign(repo, paths, extracted, pins);
        var clusters = await Clusters(repo, commit, settings.Graphify, cancellationToken).ConfigureAwait(false);
        var labels = await Labels(progress, settings, home.LabelRequests, cancellationToken).ConfigureAwait(false);
        if (labels.IsError && labels.Error.Code == ContextErrors.LabelingUnavailable.Code)
        {
            return labels.Error;
        }

        // The model names the home features (by path, never picking members); one that couldn't answer leaves them their path names. The clusters are tags, named from their files.
        var named = home.WithLabels(labels.IsError ? [] : labels.Value.Labels);
        var tags = ClusterFeatures.Propose(repo, clusters, [], extracted);
        return new RepoExtraction(extracted.Plus(named.Map).Plus(tags), commit, Degraded(clusters, labels, timedOut), await Dropped(repo, commit, cancellationToken).ConfigureAwait(false), paths);
    }

    // What the repo's ignored roles dropped from the tracked files (issue 1161); an unreadable listing counts nothing, never a failed build.
    private async ValueTask<IReadOnlyList<RoleDropped>> Dropped(ContextRepo repo, string commit, CancellationToken cancellationToken)
    {
        if (repo.Roles.All(static r => r.Demoted))
        {
            return [];
        }

        var tracked = await inputs.Repos.ListTracked(repo, commit, cancellationToken).ConfigureAwait(false);
        return tracked.IsError ? [] : RoleCounter.Dropped(repo, tracked.Value);
    }

    private static string[] Degraded(ClusterSet clusters, Result<LabelPass> labels, bool timedOut) =>
        [.. clusters is { Available: true, Reason: null } ? [] : new[] { clusters.Reason is { } why ? $"clusters: {why}" : "clusters" }, .. LabelNote(labels), .. timedOut ? new[] { "recognizers" } : []];

    private static string[] LabelNote(Result<LabelPass> labels) =>
        labels is { IsError: false, Value.Unavailable: { } why } ? [$"labels: {ContextErrors.LabelingUnavailable.Code}: {why}"]
        : labels.IsError || !labels.Value.Complete ? ["labels"] : [];

    // A denied path is never read, whatever a recognizer wants; a schema file (the globs that name where stores are declared) is read for the data-store pass.
    private static bool Readable(ContextRepo repo, (RecognizerSet Recognizers, IReadOnlyList<string> SchemaGlobs) wants, string path) =>
        !repo.Denies(path) && (Recognizers.MayRead(path) || wants.Recognizers.Wants(path) || PathDenylist.Matches(path, wants.SchemaGlobs));

    // HEAD's tree minus the denied paths, and the text of each file the extractor may read.
    private async ValueTask<Result<(string Commit, List<RepoFile> Files, IReadOnlyList<string> Paths)>> ReadFiles(IProgress<string>? progress, ContextRepo repo, (RecognizerSet Recognizers, IReadOnlyList<string> SchemaGlobs) wants, CancellationToken cancellationToken)
    {
        var head = await inputs.Repos.Head(repo, cancellationToken).ConfigureAwait(false);
        var listed = head.IsError ? head.Error : await inputs.Repos.ListFiles(repo, head.Value, cancellationToken).ConfigureAwait(false);
        if (head.IsError || listed.IsError)
        {
            return head.IsError ? head.Error : listed.Error;
        }

        var wanted = listed.Value.Where(p => Readable(repo, wants, p)).ToList();
        var files = new List<RepoFile>(wanted.Count);
        foreach (var path in wanted)
        {
            var text = await inputs.Repos.Read(repo, head.Value, path, cancellationToken).ConfigureAwait(false);
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
        return (head.Value, files, listed.Value);
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

    private async ValueTask<Result<LabelPass>> Labels(IProgress<string>? progress, ContextSettings settings, IReadOnlyList<FileCluster> requests, CancellationToken cancellationToken)
    {
        if (!settings.Labeling.Enabled || requests.Count == 0)
        {
            return Results.Success(new LabelPass([]));
        }

        progress?.Report(string.Create(CultureInfo.InvariantCulture, $"labeling {requests.Count} features"));
        return await namers.Labeler.Label(requests, settings.ExtraPatterns, settings.Labeling.MaxTokens, cancellationToken).ConfigureAwait(false);
    }

    private static void Tell(IProgress<string>? progress, int done, int total) =>
        progress?.Report(string.Create(CultureInfo.InvariantCulture, $"extracting {done}/{total} files"));

    // The concepts the last build derived are rebuilt from scratch with the new features; a person's own concept nodes (human provenance) stay.
    private static GraphSnapshot WithoutDerivedConcepts(GraphSnapshot kept)
    {
        var derived = kept.Nodes.Where(n => n.Key.Kind == NodeKind.Concept && n.Provenance != Provenance.Human).Select(n => n.Key.Value).ToHashSet(StringComparer.Ordinal);
        return derived.Count == 0
            ? kept
            : kept with { Nodes = [.. kept.Nodes.Where(n => !derived.Contains(n.Key.Value))], Edges = [.. kept.Edges.Where(e => !derived.Contains(e.To.Value) && !derived.Contains(e.From.Value))] };
    }

    // The current map first (already without what the built repos' deny globs take out, spec §6.2): what a person put there wins over what the build found, and a promote loses nothing.
    private static GraphSnapshot Join(GraphSnapshot kept, ExtractedMap found) =>
        kept with
        {
            Nodes = [.. kept.Nodes.Concat(found.Nodes).DistinctBy(n => n.Key.Value, StringComparer.Ordinal)],
            Edges = [.. kept.Edges.Concat(found.Edges).DistinctBy(e => (e.From.Value, e.To.Value, e.Type))],
        };

    private static List<ProposedNode> Proposed(GraphSnapshot current, ExtractedMap found, List<string> repoNames) =>
    [
        .. found.Nodes.Where(n => n.Key.Kind is not (NodeKind.Code or NodeKind.Record) && n is not { Provenance: Provenance.Deterministic, Key.Kind: NodeKind.Feature } && current.Resolve(n.Key) is null).OrderBy(n => n.Key.Value, StringComparer.Ordinal)
            .Select(n => new ProposedNode(n.Key, n.Provenance, n.Confidence,
                Files(found, n, repoNames))),
    ];

    // The code files behind a proposed node: those that implement it; a thing (a data store) has none, so its files are the ones that declare it.
    private static List<NodeKey> Files(ExtractedMap found, GraphNode node, List<string> repoNames) =>
        [.. (node.Key.Kind == NodeKind.Thing
            ? node.EvidenceOrEmpty.Select(e => e.Key.Value).Select(r => DeclaringFile(r, repoNames)).OfType<NodeKey>()
            : found.Edges.Where(e => e.Type is EdgeType.Implements or EdgeType.Tags && e.To.Value == node.Key.Value && e.From.Kind == NodeKind.Code).Select(e => e.From))
            .DistinctBy(k => k.Value, StringComparer.Ordinal).OrderBy(k => k.Value, StringComparer.Ordinal)];

    // "repo:<name>/<path>" -> code:<path>, stripping the exact prefix of a mapped repo (a name may hold '/').
    private static NodeKey? DeclaringFile(string record, List<string> repoNames)
    {
        var prefix = repoNames.Select(n => $"repo:{n}/").Where(p => record.StartsWith(p, StringComparison.Ordinal)).OrderByDescending(p => p.Length).FirstOrDefault();
        return prefix is not null && NodeKey.TryFrom("code:" + record[prefix.Length..], out var key) ? key : null;
    }

    private sealed record RepoExtraction(ExtractedMap Map, string Commit, IReadOnlyList<string> Degraded, IReadOnlyList<RoleDropped> Dropped, IReadOnlyList<string> Paths);
}
