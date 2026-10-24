using Aw.Context.Core;
using Aw.Context.Core.Eval.MapQuality;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;
using Aw.Context.Tests.RunBacktest;

namespace Aw.Context.Tests.EvalMap;

public sealed class EvalMapTests
{
    private static readonly DateTimeOffset _at = new(2026, 8, 1, 9, 0, 0, TimeSpan.Zero);

    private static async Task Decide(BacktestWorld world, params (OverrideAction Action, string Target)[] decisions) =>
        _ = await world.Store.AppendOverrides([.. decisions.Select((d, i) => new MapOverride(d.Action, NodeKey.From(d.Target), "avery", _at.AddMinutes(i)))], TestContext.Current.CancellationToken);

    private static Task<Kernel.Result<MapQualityRun>> Measure(BacktestWorld world, Context.Core.Engine.ContextScope? scope = null) =>
        world.Engine.EvalMap(new MapQualityRequest(scope ?? world.Human()), TestContext.Current.CancellationToken).AsTask();

    [Fact, Trait("Lane", "integration")]
    public async Task EvalMap_node_precision_is_accepted_over_accepted_plus_rejected_per_provenance()
    {
        await using var world = await BacktestWorld.Start(BacktestWorld.AfterAllCuts);
        await world.ImportMap();   // the fixture rejects its one model node (drag-to-split)
        await Decide(world, (OverrideAction.Accept, "code:src/layout/SplitPane.ts"), (OverrideAction.Accept, "code:src/layout/PaneResize.ts"), (OverrideAction.Reject, "code:src/tabs/TabManager.ts"));

        var report = (await Measure(world)).Value.Report;

        Assert.Equal((2, 3), (report.NodePrecisionDeterministic.Hits, report.NodePrecisionDeterministic.Total));
        Assert.Equal((0, 1), (report.NodePrecisionModel.Hits, report.NodePrecisionModel.Total));
        Assert.Equal((2, 4), (report.NodePrecision.Hits, report.NodePrecision.Total));
        Assert.Equal(0.5, report.NodePrecision.Value);
        Assert.Contains("node precision ≥ 0.80: not met", report.Bars, StringComparer.Ordinal);
        Assert.StartsWith(world.Settings.MapQualityDirectory, (await Measure(world)).Value.Path, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task EvalMap_without_decisions_is_not_measured_and_undecided_nodes_are_not_misses()
    {
        await using var world = await BacktestWorld.Start(BacktestWorld.AfterAllCuts);
        await world.ImportMap();
        await Decide(world, (OverrideAction.Accept, "code:src/layout/SplitPane.ts"));

        var report = (await Measure(world)).Value.Report;

        Assert.Equal((1, 1), (report.NodePrecisionDeterministic.Hits, report.NodePrecisionDeterministic.Total));
        Assert.Equal(2, report.Undecided);   // PaneResize.ts and TabManager.ts were proposed and not decided
        Assert.Null(report.LinkPrecision.Value);
        Assert.Contains("unmapped rate ≤ 0.20: not measured", report.Bars, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task EvalMap_link_precision_reads_filemap_accept_and_reject()
    {
        await using var world = await BacktestWorld.Start(BacktestWorld.AfterAllCuts);
        await world.ImportMap();
        var edges = (await world.Store.Snapshot(draft: false, TestContext.Current.CancellationToken)).Value!.Graph.Edges.Where(e => e.Type == EdgeType.Implements).Take(2).ToList();
        _ = await world.Store.AppendOverrides(
        [
            new MapOverride(OverrideAction.Accept, edges[0].From, "avery", _at, Link: LinkIds.For(edges[0])),
            new MapOverride(OverrideAction.Reject, edges[1].From, "avery", _at.AddMinutes(1), Link: LinkIds.For(edges[1])),
        ], TestContext.Current.CancellationToken);

        var report = (await Measure(world)).Value.Report;

        Assert.Equal((1, 2), (report.LinkPrecision.Hits, report.LinkPrecision.Total));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task EvalMap_unmapped_rate_uses_90_days_before_the_mapped_commit_not_wall_clock()
    {
        await using var world = await BacktestWorld.Start(BacktestWorld.AfterAllCuts);   // the wall clock is months past the history
        var last = world.Repo.PullRequests[^1];
        await world.ImportMap(commit: last.MergeCommit);

        var report = (await Measure(world)).Value.Report;

        var changed = world.Repo.PullRequests.SelectMany(p => p.Files).Distinct(StringComparer.Ordinal).ToList();
        var mapped = new[] { "src/layout/SplitPane.ts", "src/layout/PaneResize.ts", "src/tabs/TabManager.ts" };
        Assert.Equal(changed.Count, report.UnmappedRate.Total);
        Assert.Equal(changed.Count - mapped.Count(changed.Contains), report.UnmappedRate.Hits);
        Assert.Equal("2026-04-30..2026-07-29", report.Window);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task EvalMap_without_map_is_030_and_a_session_may_measure()
    {
        await using var world = await BacktestWorld.Start(BacktestWorld.AfterAllCuts);

        var none = await Measure(world);
        await world.ImportMap();
        var session = await Measure(world, world.Session());

        Assert.Equal(ContextErrors.NoMap.Code, none.Error.Code);
        Assert.True(session.IsSuccess);   // removed per owner q93 (2026-10-23)
    }

    [Fact, Trait("Lane", "integration")]
    public async Task EvalPurge_all_also_clears_probe_baselines_and_says_so()
    {
        await using var world = await BacktestWorld.Start(BacktestWorld.AfterAllCuts);
        await world.ImportMap();
        _ = await Measure(world);
        _ = Directory.CreateDirectory(world.Settings.ProbesDirectory);
        await File.WriteAllTextAsync(Path.Combine(world.Settings.ProbesDirectory, "probes-x.json"), "{}", TestContext.Current.CancellationToken);

        var kept = (await world.Engine.EvalPurge(new EvalPurgeRequest(world.Human(), All: false), TestContext.Current.CancellationToken)).Value;
        var all = (await world.Engine.EvalPurge(new EvalPurgeRequest(world.Human(), All: true), TestContext.Current.CancellationToken)).Value;
        var session = await world.Engine.EvalPurge(new EvalPurgeRequest(world.Session(), All: true), TestContext.Current.CancellationToken);

        Assert.Equal(0, kept.Total);   // nothing is 90 days old
        Assert.Equal((1, 0, 1), (all.Reports, all.CacheFiles, all.ProbeReports));
        Assert.Equal(ContextErrors.SessionsDisabled.Code, session.Error.Code);
    }
}
