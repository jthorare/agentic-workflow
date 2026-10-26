using Aw.Context.Core.Eval.Backtest;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;
using Aw.Context.Features.RunBacktest;
using Aw.Kernel;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Context.Tests.RunBacktest;

public sealed class AsOfMapTests
{
    private static async Task<CorpusSample> SampleOf(BacktestWorld world, string key)
    {
        var report = (await world.Run("graph")).Value.Report;
        return report.Samples.Single(s => s.IssueKey == key);
    }

    private static async Task<AsOfGraph> Graph(BacktestWorld world, CorpusSample sample)
    {
        var builder = world.Services.GetRequiredService<AsOfMapBuilder>();
        var repo = world.Profile.Single(r => r.Name == sample.Repo);
        return (await builder.Build(new AsOfRequest(repo, sample, world.Human()), TestContext.Current.CancellationToken)).Value;
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Backtest_graph_arm_uses_as_of_map_and_never_a_later_node()
    {
        await using var world = await BacktestWorld.Start(BacktestWorld.BeforeAllCuts);
        await world.ImportMap();

        foreach (var key in new[] { "SPL-102", "SPL-104", "SPL-105" })
        {
            var graph = (await Graph(world, await SampleOf(world, key))).Graph;
            // tabs.tab-manager arrives in PR #4 (merged 07-22): no cut before that has it, however the authored map names it
            Assert.DoesNotContain(graph.Nodes, n => string.Equals(n.Key.Value, "feature:tabs.tab-manager", StringComparison.Ordinal) || string.Equals(n.Key.Value, "code:src/tabs/TabManager.ts", StringComparison.Ordinal) || string.Equals(n.Key.Value, "action:tab-manager.restore-layout", StringComparison.Ordinal));
            Assert.DoesNotContain(graph.Edges, e => e.From.Value == "code:src/tabs/TabManager.ts" || e.To.Value == "feature:tabs.tab-manager");
        }

        var early = (await Graph(world, await SampleOf(world, "SPL-102"))).Graph;   // cut at the initial commit: SplitPane.ts does not exist yet either
        var later = (await Graph(world, await SampleOf(world, "SPL-105"))).Graph;   // cut after merge #3
        Assert.DoesNotContain(early.Nodes, n => n.Key.Value == "feature:layout.split-pane");
        Assert.Contains(later.Nodes, n => n.Key.Value == "feature:layout.split-pane");
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Backtest_applies_only_overrides_dated_before_the_cut()
    {
        await using var world = await BacktestWorld.Start(BacktestWorld.BeforeAllCuts);
        await world.ImportMap();
        // rejected on 2026-07-12: after SPL-104's issue (07-11), before SPL-105's (07-16)
        _ = await world.Store.AppendOverrides([new MapOverride(OverrideAction.Reject, NodeKey.From("feature:layout.split-pane"), "avery", new DateTimeOffset(2026, 7, 12, 9, 0, 0, TimeSpan.Zero))], TestContext.Current.CancellationToken);

        var before = (await Graph(world, await SampleOf(world, "SPL-104"))).Graph;
        var after = (await Graph(world, await SampleOf(world, "SPL-105"))).Graph;

        Assert.Contains(before.Nodes, n => n.Key.Value == "feature:layout.split-pane");
        Assert.DoesNotContain(after.Nodes, n => n.Key.Value == "feature:layout.split-pane");
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Backtest_imported_map_counts_from_its_import_date()
    {
        await using var late = await BacktestWorld.Start(BacktestWorld.AfterAllCuts);
        await late.ImportMap();   // imported in October: no July cut had it
        await using var early = await BacktestWorld.Start(BacktestWorld.BeforeAllCuts);
        await early.ImportMap();

        var without = (await Graph(late, await SampleOf(late, "SPL-105"))).Graph;
        var with = (await Graph(early, await SampleOf(early, "SPL-105"))).Graph;

        Assert.DoesNotContain(without.Nodes, n => n.Key.Value == "feature:layout.split-pane");   // only the extractor's nodes
        Assert.Contains(with.Nodes, n => n.Key.Value == "feature:layout.split-pane");
        Assert.Contains(without.Nodes, n => n.Key.Kind == NodeKind.Surface);   // the extractor still maps what the code at the cut shows
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Backtest_clusters_join_the_as_of_map_without_labels_and_the_cache_key_records_them()
    {
        await using var world = await BacktestWorld.Start(BacktestWorld.AfterAllCuts);
        var sample = await SampleOf(world, "SPL-105");
        var asked = world.Clusters.Asked.Count;

        var plain = await Graph(world, sample);
        var again = await Graph(world, sample);

        Assert.False(plain.Clustered);
        Assert.True(again.Cached);
        Assert.Equal(plain.Graph.Nodes.Select(n => n.Key.Value), again.Graph.Nodes.Select(n => n.Key.Value), StringComparer.Ordinal);
        Assert.True(world.Clusters.Asked.Count > asked);
        Assert.Empty(world.Model.Requests);   // no labeling pass, no model call (§9.1)
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Backtest_as_of_map_applies_the_deny_cut_and_drops_pending_proposals_like_the_current_map()
    {
        await using var world = await BacktestWorld.Start(BacktestWorld.BeforeAllCuts);
        await world.ImportMap();
        var sample = await SampleOf(world, "SPL-105");
        var proposal = new MapOverride(OverrideAction.Propose, NodeKey.From("feature:zoom"), "service:engine", new DateTimeOffset(2026, 7, 1, 9, 0, 0, TimeSpan.Zero), "Zoom",
            Proposal: new ProposalPayload("run-1", sample.Repo, NodeKind.Feature));
        _ = await world.Store.AppendOverrides([proposal], TestContext.Current.CancellationToken);
        var repo = world.Profile.Single(r => r.Name == sample.Repo) with { DenyPaths = ["src/layout/SplitPane.ts"] };
        var scope = world.Human() with { Repos = [repo] };

        var graph = (await world.Services.GetRequiredService<AsOfMapBuilder>().Build(new AsOfRequest(repo, sample, scope), TestContext.Current.CancellationToken)).Value.Graph;

        Assert.DoesNotContain(graph.Nodes, n => n.Key.Value == "code:src/layout/SplitPane.ts");   // the deny globs hide it, as MapAccess.View does for the current map
        Assert.DoesNotContain(graph.Nodes, n => n.Key.Value == "feature:zoom");   // a map as of a day holds no pending proposal
        Assert.Contains(graph.Nodes, n => n.Key.Kind == NodeKind.Surface);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Backtest_as_of_map_keeps_the_map_it_built_when_the_cache_write_fails()
    {
        await using var world = await BacktestWorld.Start(BacktestWorld.BeforeAllCuts);
        var sample = await SampleOf(world, "SPL-105");
        var repo = world.Profile.Single(r => r.Name == sample.Repo);
        var builder = new AsOfMapBuilder(world.Services.GetRequiredService<IRepoReader>(), world.Clusters, world.Store, new WriteFailingCache(world.Services.GetRequiredService<IEvalStore>()));

        var built = await builder.Build(new AsOfRequest(repo, sample, world.Human()), TestContext.Current.CancellationToken);

        Assert.True(built.IsSuccess, built.IsError ? built.Error.Message : null);
        Assert.False(built.Value.Cached);
        Assert.Contains(built.Value.Graph.Nodes, n => n.Key.Kind == NodeKind.Surface);
    }

    private const string FlagSection = "\"extract\":{\"recognizers\":[{\"kind\":\"flag\",\"glob\":\"src/**/*.ts\",\"pattern\":\"export (?:type|class|function|const) (?<n>[A-Za-z]+)\",\"keyFrom\":\"n\"}]}";

    [Fact, Trait("Lane", "integration")]
    public async Task Backtest_as_of_map_holds_what_a_profile_recognizer_finds_and_a_different_set_misses_the_cache()
    {
        await using var world = await BacktestWorld.Start(BacktestWorld.AfterAllCuts);
        var sample = await SampleOf(world, "SPL-105");
        var repo = world.Profile.Single(r => r.Name == sample.Repo);
        var builder = world.Services.GetRequiredService<AsOfMapBuilder>();
        var ct = TestContext.Current.CancellationToken;

        var plain = (await builder.Build(new AsOfRequest(repo, sample, world.Human()), ct)).Value;
        var withFlags = (await builder.Build(new AsOfRequest(repo, sample, world.Human(FlagSection)), ct)).Value;
        var again = (await builder.Build(new AsOfRequest(repo, sample, world.Human(FlagSection)), ct)).Value;

        Assert.DoesNotContain(plain.Graph.Nodes, n => n.Key.Value.StartsWith("surface:flag/", StringComparison.Ordinal));
        Assert.False(withFlags.Cached);   // the plain run's cached map is not served to a profile with recognizers
        Assert.Contains(withFlags.Graph.Nodes, n => n.Key.Value.StartsWith("surface:flag/", StringComparison.Ordinal));
        Assert.True(again.Cached);
        Assert.Equal(withFlags.Graph.Nodes.Select(n => n.Key.Value), again.Graph.Nodes.Select(n => n.Key.Value), StringComparer.Ordinal);
    }

    // Catastrophic on any text without the tail: every character can be matched two ways, so the one-second match timeout fires.
    // A lookahead keeps this pattern on the backtracking engine (LinearRegex's fallback), the only path that can still time out.
    private const string TimeoutSection = "\"extract\":{\"recognizers\":[{\"kind\":\"flag\",\"glob\":\"src/app.ts\",\"pattern\":\"(?=.)(?<n>(?:[^!]|[^!])+)+!!!QQ\",\"keyFrom\":\"n\"}]}";

    [Fact, Trait("Lane", "integration")]
    public async Task Backtest_as_of_map_whose_recognizer_timed_out_is_not_cached_and_the_report_says_so()
    {
        await using var world = await BacktestWorld.Start(BacktestWorld.AfterAllCuts);
        var sample = await SampleOf(world, "SPL-105");
        var repo = world.Profile.Single(r => r.Name == sample.Repo);
        var builder = world.Services.GetRequiredService<AsOfMapBuilder>();
        var ct = TestContext.Current.CancellationToken;

        var first = (await builder.Build(new AsOfRequest(repo, sample, world.Human(TimeoutSection)), ct)).Value;
        var second = (await builder.Build(new AsOfRequest(repo, sample, world.Human(TimeoutSection)), ct)).Value;
        var report = (await world.Run("graph", world.Human(TimeoutSection))).Value.Report;

        Assert.True(first.RecognizersTimedOut);
        Assert.False(second.Cached);   // the partial map was never written, so a later build extracts again
        Assert.Contains(report.Degraded, d => d.StartsWith($"recognizers timed out: {report.Samples.Count} of {report.Samples.Count} sample maps", StringComparison.Ordinal));   // every kept sample's cut has src/app.ts

        var clean = (await world.Run("graph")).Value.Report;
        Assert.DoesNotContain(clean.Degraded, d => d.StartsWith("recognizers timed out", StringComparison.Ordinal));
    }

    private const string LimitSection = "\"extract\":{\"graphify\":{\"maxFileMB\":8}}";

    [Fact, Trait("Lane", "integration")]
    public async Task Backtest_as_of_map_with_clusters_is_cached_per_limits()
    {
        await using var world = await BacktestWorld.Start(BacktestWorld.AfterAllCuts);
        world.Clusters.Set = new ClusterSet([], Available: true);
        var sample = await SampleOf(world, "SPL-105");
        var repo = world.Profile.Single(r => r.Name == sample.Repo);
        var builder = world.Services.GetRequiredService<AsOfMapBuilder>();
        var ct = TestContext.Current.CancellationToken;

        var first = (await builder.Build(new AsOfRequest(repo, sample, world.Human()), ct)).Value;
        var again = (await builder.Build(new AsOfRequest(repo, sample, world.Human()), ct)).Value;
        var other = (await builder.Build(new AsOfRequest(repo, sample, world.Human(LimitSection)), ct)).Value;

        Assert.True(first.Clustered);
        Assert.True(again.Cached);
        Assert.False(other.Cached);   // other limits can cluster other files: the default limits' map is not served
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Backtest_as_of_map_whose_cluster_pass_was_truncated_is_never_cached_as_complete()
    {
        await using var world = await BacktestWorld.Start(BacktestWorld.AfterAllCuts);
        world.Clusters.Set = new ClusterSet([], Available: true, "snapshot truncated at 2048 MB (3 files skipped)");
        var sample = await SampleOf(world, "SPL-105");
        var repo = world.Profile.Single(r => r.Name == sample.Repo);
        var builder = world.Services.GetRequiredService<AsOfMapBuilder>();
        var ct = TestContext.Current.CancellationToken;

        _ = await builder.Build(new AsOfRequest(repo, sample, world.Human()), ct);
        var second = (await builder.Build(new AsOfRequest(repo, sample, world.Human()), ct)).Value;

        Assert.False(second.Cached);
        world.Clusters.Set = new ClusterSet([], Available: true);
        Assert.False((await builder.Build(new AsOfRequest(repo, sample, world.Human()), ct)).Value.Cached);   // nothing was written under the key a whole pass uses
    }

    private sealed class WriteFailingCache(IEvalStore inner) : IEvalStore
    {
        public ValueTask<Result<string>> WriteReport<T>(EvalReportKind kind, T report, DateTimeOffset at, CancellationToken cancellationToken)
            where T : class => inner.WriteReport(kind, report, at, cancellationToken);

        public ValueTask<Result<ExtractedMap?>> ReadMap(string key, CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success<ExtractedMap?>(value: null));   // nothing cached: the map is built

        public ValueTask<Result<Unit>> WriteMap(string key, ExtractedMap map, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<Unit>>(Context.Core.ContextErrors.StoreUnavailable.With("the cache folder is read-only"));

        public ValueTask<Result<EvalPurgeReport>> Purge(DateTimeOffset now, bool all, CancellationToken cancellationToken) => inner.Purge(now, all, cancellationToken);

        public ValueTask<Result<EvalPurgeReport>> Expired(DateTimeOffset now, CancellationToken cancellationToken) => inner.Expired(now, cancellationToken);
    }
}
