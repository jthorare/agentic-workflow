using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Results;
using Aw.Context.Core.Review;
using Aw.Context.Features.BuildMap;
using Aw.Context.Testing.Extract;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.BuildMap;

public sealed class BuildMapTests : IDisposable
{
    private static readonly NodeKey _split = NodeKey.From("surface:command/split-vertical");
    private static readonly NodeKey _close = NodeKey.From("surface:command/close-pane");
    private static readonly DateTimeOffset _at = DateTimeOffset.Parse("2026-10-14T20:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
    private readonly MapWorld _world = new();

    private static ContextScope Scope => MapWorld.HumanWith(MapWorld.OnlyFixtureRepo);

    private static ContextScope Labeling(int maxTokens = 5_000) =>
        MapWorld.HumanWith("""{"repos":["aw-split-panes"],"labeling":{"enabled":true,"maxTokens":""" + maxTokens.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}}");

    private static FileCluster Cluster(params string[] files) => new(ClusterId.From("c1"), files, 0.7);

    public void Dispose() => _world.Dispose();

    private async Task<IReadOnlyList<MapVersion>> Versions() => (await _world.Store.Versions(MapWorld.None)).Value;

    [Fact, Trait("Lane", "unit")]
    public async Task A_build_writes_a_draft_with_each_proposed_node_its_provenance_confidence_and_files()
    {
        var report = (await _world.Map.BuildMap(progress: null, Scope, MapWorld.None)).Value;

        Assert.Equal((MapVersionStatus.Draft, FixtureMap.Repo, MapWorld.Head), (report.Draft.Status, report.Draft.Repo.Value, report.Draft.Commit));
        var split = Assert.Single(report.Proposed, p => p.Key.Value == _split.Value);
        Assert.Equal((Provenance.Deterministic, 1.0), (split.Provenance, split.Confidence));
        Assert.Equal(["code:src/commands/ids.ts", "code:src/commands/registry.ts"], split.Files.Select(f => f.Value), StringComparer.Ordinal);
        Assert.Contains(report.Proposed, p => p.Key.Value == "surface:setting/paneResizeStep");
        Assert.Contains(report.Proposed, p => p.Key.Value == "surface:keybinding/ctrl+shift+d");
        Assert.DoesNotContain(report.Proposed, p => p.Key.Kind == NodeKind.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_build_over_a_current_map_adds_to_it_and_proposes_only_what_it_lacks()
    {
        _ = await _world.ImportFixture();

        var report = (await _world.Map.BuildMap(progress: null, Scope, MapWorld.None)).Value;

        Assert.Equal(["surface:command/close-pane"], report.Proposed.Select(p => p.Key.Value).Where(k => k.StartsWith("surface:command/", StringComparison.Ordinal)), StringComparer.Ordinal);
        var draft = (await _world.Store.Snapshot(draft: true, MapWorld.None)).Value!;
        Assert.NotNull(draft.Graph.Resolve(NodeKey.From("feature:layout.split-pane")));   // the current map's own nodes stay, so promoting the draft loses nothing
        Assert.Contains(draft.Graph.Edges, e => e.From.Value == "code:src/commands/ids.ts" && e.To.Value == _split.Value && e.Type == EdgeType.Implements);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_build_leaves_the_current_map_alone()
    {
        var imported = await _world.ImportFixture();

        var report = (await _world.Map.BuildMap(progress: null, Scope, MapWorld.None)).Value;

        var versions = await Versions();
        Assert.Equal(imported.Version.Number, Assert.Single(versions, v => v.Status == MapVersionStatus.Current).Number);
        Assert.Equal(report.Draft.Number, Assert.Single(versions, v => v.Status == MapVersionStatus.Draft).Number);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_graphify_the_build_goes_on_with_deterministic_anchors_and_says_degraded_clusters()
    {
        var report = (await _world.Map.BuildMap(progress: null, Scope, MapWorld.None)).Value;

        Assert.Equal(["clusters"], report.Degraded);
        Assert.All(report.Proposed, p => Assert.Equal(Provenance.Deterministic, p.Provenance));
        Assert.NotEmpty(report.Proposed);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Clusters_become_candidate_features_with_model_provenance()
    {
        _world.Clusters.Set = new ClusterSet([Cluster("src/commands/ids.ts", "src/commands/registry.ts")], Available: true);

        var report = (await _world.Map.BuildMap(progress: null, Scope, MapWorld.None)).Value;

        var feature = Assert.Single(report.Proposed, p => p.Key.Kind == NodeKind.Feature);
        Assert.Equal((Provenance.Model, 0.7), (feature.Provenance, feature.Confidence));
        Assert.Equal(["code:src/commands/ids.ts", "code:src/commands/registry.ts"], feature.Files.Select(f => f.Value), StringComparer.Ordinal);
        Assert.Empty(report.Degraded);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Labeling_is_off_unless_the_profile_turns_it_on_and_the_labeler_is_never_asked()
    {
        _world.Clusters.Set = new ClusterSet([Cluster("src/commands/ids.ts", "src/commands/registry.ts")], Available: true);

        _ = await _world.Map.BuildMap(progress: null, Scope, MapWorld.None);

        Assert.Empty(_world.Labeler.Shown);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task With_labeling_on_the_models_names_label_the_clusters()
    {
        _world.Clusters.Set = new ClusterSet([Cluster("src/commands/ids.ts", "src/commands/registry.ts")], Available: true);

        _ = await _world.Map.BuildMap(progress: null, Labeling(), MapWorld.None);

        var draft = (await _world.Store.Snapshot(draft: true, MapWorld.None)).Value!;
        Assert.Equal("Labeled c1", Assert.Single(draft.Graph.Nodes, n => n.Key.Kind == NodeKind.Feature).Label);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_profiles_extra_scrub_patterns_go_to_the_labeler()
    {
        _world.Clusters.Set = new ClusterSet([Cluster("src/commands/ids.ts", "src/commands/registry.ts")], Available: true);
        var scope = MapWorld.HumanWith("""{"repos":["aw-split-panes"],"labeling":{"enabled":true},"scrub":{"extraPatterns":[{"id":"acme-customer-id","pattern":"ACME-[0-9]{6}"}]}}""");

        _ = await _world.Map.BuildMap(progress: null, scope, MapWorld.None);

        var patterns = Assert.Single(_world.Labeler.Patterns);
        Assert.Equal([("acme-customer-id", "ACME-[0-9]{6}")], patterns.Select(p => (p.Rule, p.Pattern)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_labeling_pass_over_its_cap_is_SND_CONTEXT_042_and_no_draft_appears()
    {
        _world.Clusters.Set = new ClusterSet([Cluster("src/commands/ids.ts", "src/commands/registry.ts")], Available: true);
        _world.Labeler.Failure = ContextErrors.LabelingUnavailable.With("over the cap (fix: raise 'context.labeling.maxTokens')");

        var report = await _world.Map.BuildMap(progress: null, Labeling(), MapWorld.None);

        Assert.Equal(ContextErrors.LabelingUnavailable.Code, report.Error.Code);
        Assert.Empty(await Versions());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_model_that_cant_answer_leaves_the_file_derived_names_and_says_degraded_labels()
    {
        _world.Clusters.Set = new ClusterSet([Cluster("src/commands/ids.ts", "src/commands/registry.ts")], Available: true);
        _world.Labeler.Failure = ContextErrors.SourceUnavailable.With("the model is down");

        var report = (await _world.Map.BuildMap(progress: null, Labeling(), MapWorld.None)).Value;

        Assert.Equal(["labels"], report.Degraded);
        _ = Assert.Single(await Versions());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pass_that_lost_a_later_batch_keeps_the_labels_it_got_and_says_degraded_labels()
    {
        _world.Clusters.Set = new ClusterSet([Cluster("src/commands/ids.ts", "src/commands/registry.ts")], Available: true);
        _world.Labeler.Incomplete = true;

        var report = (await _world.Map.BuildMap(progress: null, Labeling(), MapWorld.None)).Value;

        Assert.Equal(["labels"], report.Degraded);
        Assert.Equal("Labeled c1", Assert.Single((await _world.Store.Read(report.Draft.Number, MapWorld.None)).Value.Nodes, n => n.Key.Kind == NodeKind.Feature).Label);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Labeling_turned_on_with_no_labeler_set_up_is_SND_CONTEXT_042()
    {
        var labels = await new DisabledLabeler().Label([Cluster("a.ts")], [], 400_000, MapWorld.None);

        Assert.Equal(ContextErrors.LabelingUnavailable.Code, labels.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_busy_heavy_job_lock_is_SND_CONTEXT_040_naming_the_holder_and_nothing_is_written()
    {
        _world.HeavyLock.BusyHolder = "pid 4242 (bash-tests)";
        var progress = new SyncProgress();

        var report = await _world.Map.BuildMap(progress, Scope, MapWorld.None);

        Assert.Equal(ContextErrors.HeavyLockBusy.Code, report.Error.Code);
        Assert.Contains("pid 4242", report.Error.Message, StringComparison.Ordinal);
        Assert.Contains(progress.Lines, l => l.StartsWith("waiting for heavy-job lock", StringComparison.Ordinal));
        Assert.Empty(await Versions());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_slot_is_released_when_the_build_ends_and_when_it_fails()
    {
        _ = await _world.Map.BuildMap(progress: null, Scope, MapWorld.None);
        Assert.Equal((1, 0), (_world.HeavyLock.Taken, _world.HeavyLock.Held));

        var failed = await _world.Map.BuildMap(progress: null, MapWorld.Human, MapWorld.None);   // the fake repo reader knows only one of the two repos

        Assert.True(failed.IsError);
        Assert.Equal((2, 0), (_world.HeavyLock.Taken, _world.HeavyLock.Held));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_repo_git_cant_read_is_SND_CONTEXT_048_and_writes_no_draft()
    {
        var report = await _world.Map.BuildMap(progress: null, MapWorld.Human, MapWorld.None);

        Assert.Equal(ContextErrors.ExtractionUnreadable.Code, report.Error.Code);
        Assert.Empty(await Versions());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Progress_says_how_many_files_were_extracted()
    {
        var progress = new SyncProgress();

        _ = await _world.Map.BuildMap(progress, Scope, MapWorld.None);

        Assert.Equal("extracting 5/5 files", progress.Lines.Last(l => l.StartsWith("extracting ", StringComparison.Ordinal)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_cancelled_build_leaves_no_draft()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await _world.Map.BuildMap(progress: null, Scope, cancelled.Token));

        Assert.Empty(await Versions());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_may_build()
    {
        var report = await _world.Map.BuildMap(progress: null, MapWorld.Session(FixtureMap.Repo), MapWorld.None);

        Assert.True(report.IsSuccess);
        Assert.NotEmpty(await Versions());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_second_build_keeps_every_decision_and_re_proposes_no_rejected_node()
    {
        _ = await _world.Map.BuildMap(progress: null, Scope, MapWorld.None);
        var first = (await _world.Store.Snapshot(draft: true, MapWorld.None)).Value!;
        var rejected = first.Graph.Resolve(_close)!;
        var proposed = new MapOverride(OverrideAction.Propose, NodeKey.From("feature:tabs.restore"), "service:engine", _at, "Restore tabs",
            Proposal: new ProposalPayload("run-1", ContextRepoName.From(FixtureMap.Repo), NodeKind.Feature));
        _ = await _world.Store.AppendOverrides(
        [
            new MapOverride(OverrideAction.Accept, _split, "avery", _at),
            new MapOverride(OverrideAction.Reject, _close, "avery", _at, Rejection: RejectionIdentity.For(rejected)),
            proposed,
        ], MapWorld.None);

        var second = (await _world.Map.BuildMap(progress: null, Scope, MapWorld.None)).Value;

        var snapshot = (await _world.Store.Snapshot(draft: true, MapWorld.None)).Value!;
        var applied = OverrideApplier.Apply(snapshot.Graph, snapshot.Overrides).Graph;
        Assert.NotEqual(first.Version.Number, second.Draft.Number);
        Assert.Equal(Provenance.Human, applied.Resolve(_split)!.Provenance);
        Assert.Null(applied.Resolve(_close));
        Assert.NotNull(applied.Resolve(NodeKey.From("feature:tabs.restore")));
        _ = Assert.Single(await Versions(), v => v.Status == MapVersionStatus.Draft);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Denied_paths_never_reach_the_recognizers_the_clusters_or_the_labeler()
    {
        var files = new Dictionary<string, string>(MapWorld.BuildFiles, StringComparer.Ordinal) { ["private/settings.json"] = "{\"apiToken\": \"hunter2\"}" };
        var reader = new LeakyRepoReader(MapWorld.Head, files);
        using var world = new MapWorld(reader);
        var repo = new ContextRepo(ContextRepoName.From(FixtureMap.Repo), "/work/aw-split-panes") { DenyPaths = [.. PathDenylist.Defaults, "private/**"] };
        world.Clusters.Set = new ClusterSet([Cluster("src/commands/ids.ts", "private/settings.json", "config/secrets/settings.json")], Available: true);

        var report = (await world.Map.BuildMap(progress: null, MapWorld.HumanWith("""{"repos":["aw-split-panes"],"labeling":{"enabled":true}}""", [repo]), MapWorld.None)).Value;

        Assert.DoesNotContain(reader.Reads, r => string.Equals(r, "private/settings.json", StringComparison.Ordinal) || string.Equals(r, ".env", StringComparison.Ordinal) || string.Equals(r, "config/secrets/settings.json", StringComparison.Ordinal));
        Assert.DoesNotContain(report.Proposed, p => p.Key.Value == "surface:setting/apiToken");
        Assert.DoesNotContain(report.Proposed.SelectMany(p => p.Files), f => f.Value.Contains("private/", StringComparison.Ordinal) || f.Value.Contains("secrets/", StringComparison.Ordinal));
        Assert.Equal(["src/commands/ids.ts"], world.Labeler.Shown.SelectMany(c => c.Files), StringComparer.Ordinal);
        var draft = (await world.Store.Snapshot(draft: true, MapWorld.None)).Value!;
        Assert.DoesNotContain(draft.Graph.Nodes, n => n.Key.Value.Contains("private/", StringComparison.Ordinal) || n.Key.Value.Contains("secrets/", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_deny_glob_added_after_a_map_exists_removes_the_denied_code_nodes_their_edges_and_the_features_only_they_made()
    {
        _world.Clusters.Set = new ClusterSet([Cluster("src/commands/ids.ts", "src/commands/registry.ts")], Available: true);
        var first = (await _world.Map.BuildMap(progress: null, Scope, MapWorld.None)).Value;
        _ = await _world.Store.Promote(first.Draft.Number, MapWorld.None);
        var current = (await _world.Store.Snapshot(draft: false, MapWorld.None)).Value!;
        Assert.Contains(current.Graph.Nodes, n => n.Key.Value == "code:src/commands/ids.ts");
        Assert.Contains(current.Graph.Nodes, n => n.Key.Kind == NodeKind.Feature);
        var denying = new ContextRepo(ContextRepoName.From(FixtureMap.Repo), "/work/aw-split-panes") { DenyPaths = [.. PathDenylist.Defaults, "src/commands/**"] };

        _ = await _world.Map.BuildMap(progress: null, MapWorld.HumanWith(MapWorld.OnlyFixtureRepo, [denying]), MapWorld.None);

        var draft = (await _world.Store.Snapshot(draft: true, MapWorld.None)).Value!;
        Assert.DoesNotContain(draft.Graph.Nodes, n => n.Key.Value.StartsWith("code:src/commands/", StringComparison.Ordinal));
        Assert.DoesNotContain(draft.Graph.Edges, e => e.From.Value.StartsWith("code:src/commands/", StringComparison.Ordinal) || e.To.Value.StartsWith("code:src/commands/", StringComparison.Ordinal));
        Assert.DoesNotContain(draft.Graph.Nodes, n => n.Key.Kind == NodeKind.Feature && n.Provenance == Provenance.Model);
        Assert.Contains(draft.Graph.Nodes, n => n.Key.Value == "code:src/keys.ts");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_rebuild_after_a_deny_glob_undoes_the_accepts_about_what_it_cut_so_the_draft_has_no_orphan_and_promotes()
    {
        var first = (await _world.Map.BuildMap(progress: null, Scope, MapWorld.None)).Value;
        Assert.True((await _world.Map.ApplyReview(new ReviewDecision(ReviewVerb.AcceptDeterministic), Scope, MapWorld.None)).IsSuccess);
        Assert.True((await _world.Map.Promote(Scope, MapWorld.None)).IsSuccess);
        var denying = MapWorld.HumanWith(MapWorld.OnlyFixtureRepo, [new ContextRepo(ContextRepoName.From(FixtureMap.Repo), "/work/aw-split-panes") { DenyPaths = [.. PathDenylist.Defaults, "src/commands/**"] }]);
        Assert.Contains(first.Proposed, p => p.Key.Value == _split.Value);

        _ = await _world.Map.BuildMap(progress: null, denying, MapWorld.None);

        var queue = (await _world.Map.ReviewQueue(limit: null, denying, MapWorld.None)).Value;
        var log = (await _world.Store.Overrides(MapWorld.None)).Value;
        Assert.DoesNotContain(queue.Entries, e => e.Group == ReviewGroup.Orphaned);
        Assert.Contains(log, o => o.IsCutUndo && o.Target.Value == _split.Value);
        Assert.DoesNotContain(OverrideLog.Active(log), o => o.Target.Value == _split.Value);
        var promoted = await _world.Map.Promote(denying, MapWorld.None);
        Assert.True(promoted.IsSuccess, promoted.IsError ? promoted.Error.Message : null);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_rebuild_after_a_deny_glob_also_undoes_the_decisions_about_a_surface_only_the_open_draft_holds()
    {
        _ = await _world.Map.BuildMap(progress: null, Scope, MapWorld.None);   // no promote: the surface lives in the open draft only
        Assert.True((await _world.Map.ApplyReview(new ReviewDecision(ReviewVerb.Accept, _split), Scope, MapWorld.None)).IsSuccess);
        Assert.True((await _world.Map.ApplyReview(new ReviewDecision(ReviewVerb.Rename, _close, Label: "Close it"), Scope, MapWorld.None)).IsSuccess);
        var denying = MapWorld.HumanWith(MapWorld.OnlyFixtureRepo, [new ContextRepo(ContextRepoName.From(FixtureMap.Repo), "/work/aw-split-panes") { DenyPaths = [.. PathDenylist.Defaults, "src/commands/**"] }]);

        _ = await _world.Map.BuildMap(progress: null, denying, MapWorld.None);

        var queue = (await _world.Map.ReviewQueue(limit: null, denying, MapWorld.None)).Value;
        var history = (await _world.Map.ReviewHistory(limit: null, denying, MapWorld.None)).Value;
        Assert.True((await _world.Map.ApplyReview(new ReviewDecision(ReviewVerb.AcceptDeterministic), denying, MapWorld.None)).IsSuccess);
        var promoted = await _world.Map.Promote(denying, MapWorld.None);
        Assert.True(promoted.IsSuccess, promoted.IsError ? promoted.Error.Message : null);
        var exported = await _world.Map.ExportMap(path: null, denying, MapWorld.None);
        var document = _world.Exports.Documents[exported.Value.Path];
        var names = history.Entries.SelectMany(e => new[] { e.Target.Value, e.Label ?? string.Empty })
            .Concat((document.Overrides ?? []).Select(o => o.Target.Value)).Concat((document.Orphaned ?? []).Select(k => k.Value)).Concat(document.Nodes.Select(n => n.Key.Value));
        Assert.DoesNotContain(queue.Entries, e => e.Group == ReviewGroup.Orphaned);
        Assert.DoesNotContain(names, n => n.Contains("split-vertical", StringComparison.Ordinal) || n.Contains("close-pane", StringComparison.Ordinal) || n.Contains("Close it", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_build_whose_draft_cant_be_saved_writes_no_undo_rows()
    {
        _ = await _world.Map.BuildMap(progress: null, Scope, MapWorld.None);
        Assert.True((await _world.Map.ApplyReview(new ReviewDecision(ReviewVerb.AcceptDeterministic), Scope, MapWorld.None)).IsSuccess);
        Assert.True((await _world.Map.Promote(Scope, MapWorld.None)).IsSuccess);
        var denying = MapWorld.HumanWith(MapWorld.OnlyFixtureRepo, [new ContextRepo(ContextRepoName.From(FixtureMap.Repo), "/work/aw-split-panes") { DenyPaths = [.. PathDenylist.Defaults, "src/commands/**"] }]);
        var before = (await _world.Store.Overrides(MapWorld.None)).Value.Count;
        _world.Store.SaveDraftFailure = ContextErrors.StoreUnavailable.With("disk full");

        var failed = await _world.Map.BuildMap(progress: null, denying, MapWorld.None);

        Assert.True(failed.IsError);
        Assert.Equal(before, (await _world.Store.Overrides(MapWorld.None)).Value.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_cluster_source_that_fails_reads_like_an_absent_one()
    {
        _world.Clusters.Failure = ContextErrors.SourceUnavailable.With("graphify crashed");

        var report = (await _world.Map.BuildMap(progress: null, Scope, MapWorld.None)).Value;

        Assert.Equal(["clusters"], report.Degraded);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_big_repo_reports_progress_every_fifty_files_and_at_the_end()
    {
        var files = Enumerable.Range(0, 120).ToDictionary(i => "src/c" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".ts", i => "export type CommandId = \"cmd-" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\";", StringComparer.Ordinal);
        using var big = new MapWorld(new FakeRepoReader(MapWorld.Repos[0], MapWorld.Head, files));
        var progress = new SyncProgress();

        _ = await big.Map.BuildMap(progress, Scope, MapWorld.None);

        Assert.Equal(["extracting 50/120 files", "extracting 100/120 files", "extracting 120/120 files"], progress.Lines.Where(l => l.StartsWith("extracting ", StringComparison.Ordinal)), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_file_git_cant_read_part_way_is_SND_CONTEXT_048_and_writes_no_draft()
    {
        var reader = new LeakyRepoReader(MapWorld.Head, new Dictionary<string, string>(StringComparer.Ordinal) { ["src/a.ts"] = "export type CommandId = \"a\";" });
        using var broken = new MapWorld(new VanishingRepoReader(reader, "src/a.ts"));

        var report = await broken.Map.BuildMap(progress: null, Scope, MapWorld.None);

        Assert.Equal(ContextErrors.ExtractionUnreadable.Code, report.Error.Code);
        Assert.Empty((await broken.Store.Versions(MapWorld.None)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Every_mapped_repo_joins_one_draft_named_for_the_first()
    {
        var other = new ContextRepo(ContextRepoName.From("other"), "/work/other");
        var routing = new RoutingRepoReader(new Dictionary<string, IRepoReader>(StringComparer.Ordinal)
        {
            [FixtureMap.Repo] = new FakeRepoReader(MapWorld.Repos[0], MapWorld.Head, MapWorld.BuildFiles),
            ["other"] = new FakeRepoReader(other, "fedcba9876543210fedcba9876543210fedcba98", new Dictionary<string, string>(StringComparer.Ordinal) { ["src/other.ts"] = "export type CommandId = \"only-here\";" }),
        });
        using var both = new MapWorld(routing);

        var report = (await both.Map.BuildMap(progress: null, MapWorld.Human, MapWorld.None)).Value;

        Assert.Equal((FixtureMap.Repo, MapWorld.Head), (report.Draft.Repo.Value, report.Draft.Commit));
        Assert.Contains(report.Proposed, p => p.Key.Value == "surface:command/only-here");
        Assert.Contains(report.Proposed, p => p.Key.Value == "surface:command/split-vertical");
    }
}
