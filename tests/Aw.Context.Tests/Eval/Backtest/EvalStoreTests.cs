using Aw.Context.Core;
using Aw.Context.Core.Eval;
using Aw.Context.Core.Eval.Backtest;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;
using Aw.Context.Infrastructure.Eval;
using Aw.Context.Infrastructure.Store;
using Aw.Context.Tests.Infrastructure;

namespace Aw.Context.Tests.Eval.Backtest;

public sealed class EvalStoreTests : IDisposable
{
    private static readonly DateTimeOffset _now = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);
    private readonly string _root = Directory.CreateTempSubdirectory("aw-eval-store-").FullName;
    private readonly ContextStoreSettings _settings;

    public EvalStoreTests()
    {
        _settings = new ContextStoreSettings(Path.Combine(_root, "context"));
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private FileEvalStore Store(ContextStoreSettings? settings = null) => new(settings ?? _settings);

    private static ProbeReport Report() => new(ProbeReport.CurrentSchemaVersion, "probes.json", "hash", MapVersion: null, [], new ProbeTotals(0, 0, 0, _now), Baseline: null, Regressed: false);

    private static ExtractedMap Map() =>
        new([new GraphNode(NodeKey.From("feature:a.one"), "one", Provenance.Deterministic), new GraphNode(NodeKey.From("code:src/a.ts"), "a.ts", Provenance.Deterministic)],
            [new GraphEdge(NodeKey.From("code:src/a.ts"), NodeKey.From("feature:a.one"), EdgeType.Implements, Provenance.Deterministic, 1.0, [])]);

    [Fact, Trait("Lane", "integration")]
    public async Task AsOfMapCache_round_trips_and_keys_on_sha_extractor_version_and_clusters()
    {
        var store = Store();
        var repo = Context.Core.Engine.ContextRepoName.From("aw-split-panes");
        var plain = AsOfMapCache.Key(repo, "0123456789abcdef0123", MapExtractor.Version, clustered: false);
        var clustered = AsOfMapCache.Key(repo, "0123456789abcdef0123", MapExtractor.Version, clustered: true);

        Assert.Null((await store.ReadMap(plain, TestContext.Current.CancellationToken)).Value);
        _ = await store.WriteMap(plain, Map(), TestContext.Current.CancellationToken);
        var read = (await store.ReadMap(plain, TestContext.Current.CancellationToken)).Value;

        Assert.Equal("aw-split-panes-0123456789ab-x6-c0", plain);
        Assert.Equal("aw-split-panes-0123456789ab-x6-c1", clustered);
        Assert.Null((await store.ReadMap(clustered, TestContext.Current.CancellationToken)).Value);   // a machine without graphify never reuses a clustered map, and the reverse
        Assert.Equal(Map().Nodes.Select(n => n.Key.Value), read!.Nodes.Select(n => n.Key.Value), StringComparer.Ordinal);
        Assert.Equal(Map().Edges.Select(e => e.To.Value), read.Edges.Select(e => e.To.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_store_serialises_the_real_backtest_and_map_quality_reports_and_the_map_cache_through_its_own_context()
    {
        var store = Store();
        var backtest = new BacktestReport(1, _now, "all", 5, SmallSample: true, 1.0, new LeftOut(1, 0, 0, ["SPL-106"]), [], [], Verdicts.OwnerDecision, ["clusters unavailable: 1 of 5 sample maps were built without clusters, so the map-reading arms ran on thinner maps (fix: install graphify, or sindri doctor)"]);
        var quality = new Context.Core.Eval.MapQuality.MapQualityReport(1, _now, "v1", new(4, 5), new(3, 3), new(1, 2), new(2, 2), 0, new(1, 10), Window: null, ["node precision: met"]);

        var b = await store.WriteReport(EvalReportKind.Backtest, backtest, _now, TestContext.Current.CancellationToken);
        var m = await store.WriteReport(EvalReportKind.Map, quality, _now, TestContext.Current.CancellationToken);

        Assert.Contains("\"sampleCount\":5", await File.ReadAllTextAsync(b.Value, TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.True(m.IsSuccess);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_report_is_written_0600_in_0700_folders_under_context_eval()
    {
        var store = Store();

        var path = (await store.WriteReport(EvalReportKind.Backtest, Report(), _now, TestContext.Current.CancellationToken)).Value;
        _ = await store.WriteMap("k", Map(), TestContext.Current.CancellationToken);

        Assert.Equal(Path.Combine(_settings.BacktestDirectory, "backtest-20261016T090000000Z.json"), path);
        Assert.Contains(Path.Combine("eval", "backtest"), path, StringComparison.Ordinal);
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
            foreach (var dir in (string[])[_settings.EvalDirectory, _settings.BacktestDirectory, _settings.MapCacheDirectory])
            {
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(dir));
            }
        }
    }

    [Fact, Trait("Lane", "integration")]
    public async Task BacktestReportStore_writes_0600_in_0700_and_refuses_a_worktree_with_005()
    {
        using var repo = new TempGitRepo(new Dictionary<string, string>(StringComparer.Ordinal) { ["a.txt"] = "a" });
        var inside = new ContextStoreSettings(Path.Combine(repo.Path, "ctx"));

        var report = await Store(inside).WriteReport(EvalReportKind.Backtest, Report(), _now, TestContext.Current.CancellationToken);
        var cache = await Store(inside).WriteMap("k", Map(), TestContext.Current.CancellationToken);

        Assert.Equal(ContextErrors.PathInWorktree.Code, report.Error.Code);
        Assert.Equal(ContextErrors.PathInWorktree.Code, cache.Error.Code);
        Assert.False(Directory.Exists(Path.Combine(repo.Path, "ctx")));   // nothing was written
    }

    [Fact, Trait("Lane", "integration")]
    public async Task EvalPurge_deletes_reports_and_cache_past_90_days_only()
    {
        var store = Store();
        var old = (await store.WriteReport(EvalReportKind.Backtest, Report(), _now.AddDays(-100), TestContext.Current.CancellationToken)).Value;
        var fresh = (await store.WriteReport(EvalReportKind.Map, Report(), _now, TestContext.Current.CancellationToken)).Value;
        _ = await store.WriteMap("old", Map(), TestContext.Current.CancellationToken);
        _ = await store.WriteMap("fresh", Map(), TestContext.Current.CancellationToken);
        File.SetLastWriteTimeUtc(old, _now.UtcDateTime.AddDays(-91));
        File.SetLastWriteTimeUtc(Path.Combine(_settings.MapCacheDirectory, "old.json"), _now.UtcDateTime.AddDays(-91));

        var expired = (await store.Expired(_now, TestContext.Current.CancellationToken)).Value;
        var purged = (await store.Purge(_now, all: false, TestContext.Current.CancellationToken)).Value;

        Assert.Equal((1, 1, 0), (expired.Reports, expired.CacheFiles, expired.ProbeReports));
        Assert.Equal(expired, purged);
        Assert.False(File.Exists(old));
        Assert.True(File.Exists(fresh));
        Assert.True(File.Exists(Path.Combine(_settings.MapCacheDirectory, "fresh.json")));
        Assert.False(File.Exists(Path.Combine(_settings.MapCacheDirectory, "old.json")));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task EvalPurge_all_clears_everything_including_probe_reports()
    {
        var store = Store();
        _ = await store.WriteReport(EvalReportKind.Backtest, Report(), _now, TestContext.Current.CancellationToken);
        _ = await store.WriteMap("k", Map(), TestContext.Current.CancellationToken);
        _ = Directory.CreateDirectory(_settings.ProbesDirectory);
        await File.WriteAllTextAsync(Path.Combine(_settings.ProbesDirectory, "probes-x.json"), "{}", TestContext.Current.CancellationToken);

        var purged = (await store.Purge(_now, all: true, TestContext.Current.CancellationToken)).Value;

        Assert.Equal((1, 1, 1), (purged.Reports, purged.CacheFiles, purged.ProbeReports));
        Assert.Empty(Directory.EnumerateFiles(_settings.EvalDirectory, "*.json", SearchOption.AllDirectories));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Purge_of_a_home_with_no_eval_folder_removes_nothing()
    {
        var purged = (await Store().Purge(_now, all: true, TestContext.Current.CancellationToken)).Value;

        Assert.Equal(0, purged.Total);
    }
}
