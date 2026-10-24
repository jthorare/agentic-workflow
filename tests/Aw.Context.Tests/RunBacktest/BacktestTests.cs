using Aw.Context.Core;
using Aw.Context.Core.Eval.Backtest;
using Aw.Context.Core.Extract;

namespace Aw.Context.Tests.RunBacktest;

public sealed class BacktestTests
{
    private static ArmReport Arm(BacktestReport report, string name) => report.Arms.Single(a => a.Arm == name);

    [Fact, Trait("Lane", "integration")]
    public async Task Backtest_on_split_panes_prints_five_samples_small_sample_and_owner_decision()
    {
        await using var world = await BacktestWorld.Start(BacktestWorld.BeforeAllCuts);
        await world.ImportMap();

        var run = (await world.Run()).Value;

        var report = run.Report;
        Assert.Equal(["SPL-101", "SPL-102", "SPL-103", "SPL-104", "SPL-105"], report.Samples.Select(s => s.IssueKey), StringComparer.Ordinal);
        Assert.Equal((5, true, 1), (report.SampleCount, report.SmallSample, report.LeftOut.NoMerge));
        Assert.Equal(["SPL-106"], report.LeftOut.Keys);   // no merged PR
        Assert.Equal(1.0, report.LeakyShare);   // every fixture issue was edited after its cut
        Assert.Equal(["brief", "docs", "graph", "graph+reviewed", "all", "linear", "onyx", "code"], report.Arms.Select(a => a.Arm), StringComparer.Ordinal);
        Assert.Equal(["S-6", "S-7B", "S-9"], report.Arms.Select(a => a.NotAvailable).OfType<string>(), StringComparer.Ordinal);
        Assert.All(report.Arms.Where(a => a.NotAvailable is null), a => Assert.Equal(Verdicts.OwnerDecision, a.PassLine!.Overall));
        Assert.Equal(Verdicts.OwnerDecision, report.Overall);
        Assert.StartsWith(world.Settings.BacktestDirectory, run.Path, StringComparison.Ordinal);
        Assert.True(File.Exists(run.Path));
        var cut = report.Samples.ToDictionary(s => s.IssueKey, s => s.Cut, StringComparer.Ordinal);
        Assert.Equal(cut["SPL-101"], cut["SPL-102"]);   // both cut at the initial commit
        Assert.NotEqual(cut["SPL-103"], cut["SPL-104"], StringComparer.Ordinal);
        Assert.Equal(report.Samples.Select(s => s.IssueKey), Arm(report, "graph").Samples.Select(s => s.IssueKey), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Backtest_graph_reviewed_arm_reads_current_map_and_is_labeled_leaky()
    {
        await using var world = await BacktestWorld.Start(BacktestWorld.BeforeAllCuts);
        await world.ImportMap();

        var report = (await world.Run()).Value.Report;

        var ceiling = Arm(report, "graph+reviewed");
        Assert.True(ceiling.Leaky);
        Assert.Equal(1.0, ceiling.Score!.LeakyShare);
        Assert.All(ceiling.Samples, s => Assert.True(s.Leaky));
        Assert.False(Arm(report, "graph").Leaky);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Backtest_makes_no_model_call_with_adjudicator_off()
    {
        await using var world = await BacktestWorld.Start(BacktestWorld.BeforeAllCuts);
        await world.ImportMap();

        var report = (await world.Run()).Value.Report;

        Assert.Empty(world.Model.Requests);
        Assert.All(report.Arms.Where(a => a.Score is not null), a => Assert.Equal(0, a.Score!.Tokens));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Backtest_takes_one_heavy_slot_and_busy_lock_is_040()
    {
        await using var world = await BacktestWorld.Start(BacktestWorld.BeforeAllCuts);
        _ = await world.Run("graph");
        Assert.Equal((1, 0), (world.HeavyLock.Taken, world.HeavyLock.Held));   // one slot for the whole run, released after

        world.HeavyLock.BusyHolder = "pid 4242";
        var busy = await world.Run("graph");

        Assert.Equal(ContextErrors.HeavyLockBusy.Code, busy.Error.Code);
        Assert.Contains("pid 4242", busy.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Backtest_session_caller_is_not_refused()
    {
        await using var world = await BacktestWorld.Start(BacktestWorld.BeforeAllCuts);

        var run = await world.Run("graph", world.Session());

        Assert.NotEqual(ContextErrors.SessionsDisabled.Code, run.IsError ? run.Error.Code : string.Empty, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Backtest_without_brief_source_is_081_and_with_no_eligible_sample_is_080()
    {
        await using var world = await BacktestWorld.Start(BacktestWorld.BeforeAllCuts);

        var none = await world.Run("graph", world.Human(onyx: false));
        var empty = await world.Run("graph", configure: r => r with { Until = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero) });
        var later = await world.Run("graph", configure: r => r with { Since = new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero) });   // the source itself lists nothing updated since

        Assert.Equal(ContextErrors.NoBriefSource.Code, none.Error.Code);
        Assert.Equal(ContextErrors.NoEligibleSample.Code, empty.Error.Code);
        Assert.Equal(ContextErrors.NoEligibleSample.Code, later.Error.Code);
        Assert.Contains("5", empty.Error.Message, StringComparison.Ordinal);   // the counts: what the window cut away is named
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Backtest_window_and_limit_cut_the_samples()
    {
        await using var world = await BacktestWorld.Start(BacktestWorld.BeforeAllCuts);

        var until = (await world.Run("graph", configure: r => r with { Until = new DateTimeOffset(2026, 7, 2, 23, 0, 0, TimeSpan.Zero) })).Value.Report;
        var limited = (await world.Run("graph", configure: r => r with { Limit = 2 })).Value.Report;

        Assert.Equal(["SPL-101", "SPL-102", "SPL-103"], until.Samples.Select(s => s.IssueKey), StringComparer.Ordinal);
        Assert.Equal(["SPL-101", "SPL-102"], limited.Samples.Select(s => s.IssueKey), StringComparer.Ordinal);
        Assert.True(limited.SmallSample);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Backtest_cancelled_writes_no_report()
    {
        await using var world = await BacktestWorld.Start(BacktestWorld.BeforeAllCuts);
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await world.RunWith(cancel.Token, "graph"));

        Assert.False(Directory.Exists(world.Settings.BacktestDirectory) && Directory.EnumerateFiles(world.Settings.BacktestDirectory).Any());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Backtest_extract_failure_at_a_cut_drops_the_sample_with_048_counted()
    {
        await using var world = await BacktestWorld.Start(BacktestWorld.BeforeAllCuts, wrap: inner => new FailingReader(inner));
        await world.ImportMap();

        var report = (await world.Run("graph")).Value.Report;

        Assert.DoesNotContain(report.Samples, s => s.IssueKey == "SPL-104");
        Assert.Equal(1, report.LeftOut.ExtractFailed);
        Assert.Equal(4, report.SampleCount);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Backtest_report_holds_refs_and_numbers_never_brief_text()
    {
        await using var world = await BacktestWorld.Start(BacktestWorld.BeforeAllCuts);
        await world.ImportMap();

        var run = (await world.Run()).Value;

        var text = await File.ReadAllTextAsync(run.Path, TestContext.Current.CancellationToken);
        Assert.DoesNotContain("named command in one registry", text, StringComparison.Ordinal);   // SPL-101's brief
        Assert.DoesNotContain("Command registry for pane commands", text, StringComparison.Ordinal);   // and its title
        Assert.Contains("onyx/0e5a1b00-0000-4000-8000-000000000101", text, StringComparison.Ordinal);   // the ref is there
        Assert.DoesNotContain(world.Onyx.Token, text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Backtest_is_deterministic_and_a_second_run_reads_the_map_cache()
    {
        await using var world = await BacktestWorld.Start(BacktestWorld.BeforeAllCuts);
        await world.ImportMap();
        var first = new ListProgress();
        var second = new ListProgress();

        var one = (await world.Run("all", configure: r => r with { Progress = first })).Value.Report;
        var two = (await world.Run("all", configure: r => r with { Progress = second })).Value.Report;

        Assert.Equal(one.Arms.Select(a => a.Score), two.Arms.Select(a => a.Score));
        Assert.Contains("corpus: 5 samples", first.Lines, StringComparer.Ordinal);
        Assert.Contains("sample 1/5 SPL-101: map built", first.Lines, StringComparer.Ordinal);
        Assert.Contains("sample 3/5 SPL-103: map cached", first.Lines, StringComparer.Ordinal);   // SPL-101..103 share the initial commit as their cut
        Assert.All(second.Lines.Where(l => l.StartsWith("sample ", StringComparison.Ordinal) && l.Contains(": map", StringComparison.Ordinal)), l => Assert.EndsWith("map cached", l, StringComparison.Ordinal));
        Assert.Contains(first.Lines, l => l.StartsWith("arm graph SPL-103", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Backtest_with_the_adjudicator_on_judges_untouched_surfaces_and_reports_adjudicated_precision()
    {
        await using var world = await BacktestWorld.Start(BacktestWorld.BeforeAllCuts);
        await world.ImportMap();
        world.Model.Responder = request => "[" + string.Join(',', request.Prompt.Split('\n').Where(l => l.Length > 2 && l[0] == 'n' && char.IsDigit(l[1])).Select(l => $"{{\"id\":\"{l[..l.IndexOf(':', StringComparison.Ordinal)]}\",\"relevant\":true}}")) + "]";

        var report = (await world.Run("graph", scope: world.Human("\"eval\":{\"adjudicator\":true}"))).Value.Report;

        var graph = Arm(report, "graph").Score!;
        Assert.NotEmpty(world.Model.Requests);
        Assert.All(world.Model.Requests, r => Assert.DoesNotContain("code:", r.Prompt, StringComparison.Ordinal));
        Assert.True(graph.Tokens > 0);
        Assert.True(graph.AdjudicatedPrecision >= graph.NodePrecision);
        Assert.DoesNotContain(report.Degraded, d => d.Contains("adjudication", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Backtest_says_how_many_sample_maps_were_built_without_clusters_from_real_state()
    {
        await using var without = await BacktestWorld.Start(BacktestWorld.BeforeAllCuts);   // the default cluster source is unavailable
        await using var with = await BacktestWorld.Start(BacktestWorld.BeforeAllCuts);
        with.Clusters.Set = new ClusterSet([], Available: true);

        var plain = (await without.Run("graph")).Value.Report;
        var clustered = (await with.Run("graph")).Value.Report;

        Assert.Contains(plain.Degraded, d => d.StartsWith($"clusters unavailable: {plain.Samples.Count} of {plain.Samples.Count} sample maps were built without clusters", StringComparison.Ordinal));
        Assert.DoesNotContain(clustered.Degraded, d => d.StartsWith("clusters unavailable", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Backtest_of_the_docs_arm_alone_does_not_report_missing_clusters()
    {
        await using var world = await BacktestWorld.Start(BacktestWorld.BeforeAllCuts);   // no clusters, but the docs arm reads no map

        var report = (await world.Run("docs")).Value.Report;

        Assert.DoesNotContain(report.Degraded, d => d.StartsWith("clusters unavailable", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Backtest_says_so_when_a_teams_issue_listing_hit_the_source_cap()
    {
        await using var world = await BacktestWorld.Start(BacktestWorld.BeforeAllCuts);
        world.Clusters.Set = new ClusterSet([], Available: true);
        world.Briefs.Capped.Add("SPL");

        var report = (await world.Run("graph")).Value.Report;

        Assert.Contains(report.Degraded, d => d.StartsWith("issue listing capped: SPL", StringComparison.Ordinal) && d.Contains("not in this corpus", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Backtest_with_no_eligible_sample_in_a_capped_listing_says_the_listing_was_capped()
    {
        await using var world = await BacktestWorld.Start(BacktestWorld.BeforeAllCuts);
        world.Briefs.Capped.Add("SPL");

        var run = await world.Run("graph", configure: r => r with { Since = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero) });

        Assert.Equal(ContextErrors.NoEligibleSample.Code, run.Error.Code);
        Assert.Contains("listing capped: SPL", run.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Backtest_counts_and_reports_samples_dropped_because_the_brief_source_was_down()
    {
        await using var world = await BacktestWorld.Start(BacktestWorld.BeforeAllCuts);
        world.Briefs.Down.UnionWith(["SPL-102", "SPL-104"]);

        var report = (await world.Run("graph")).Value.Report;

        Assert.Equal(["SPL-101", "SPL-103", "SPL-105"], report.Samples.Select(s => s.IssueKey), StringComparer.Ordinal);
        Assert.Equal(2, report.LeftOut.SourceDown);
        Assert.Equal(3, report.LeftOut.Total);   // SPL-106 has no merged PR; the two dropped for the source join it
        Assert.Contains(report.Degraded, d => d.Contains("SND-CONTEXT-011", StringComparison.Ordinal) && d.Contains('2', StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Backtest_with_every_sample_dropped_is_080_naming_why_not_an_empty_report()
    {
        await using var world = await BacktestWorld.Start(BacktestWorld.BeforeAllCuts);
        world.Briefs.Down.UnionWith(["SPL-101", "SPL-102", "SPL-103", "SPL-104", "SPL-105"]);

        var run = await world.Run("graph");

        Assert.Equal(ContextErrors.NoEligibleSample.Code, run.Error.Code);
        Assert.Contains("5 dropped for an unavailable brief source (SND-CONTEXT-011)", run.Error.Message, StringComparison.Ordinal);   // the why-counts, not the template
        Assert.False(Directory.Exists(world.Settings.BacktestDirectory) && Directory.EnumerateFiles(world.Settings.BacktestDirectory).Any());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Backtest_a_context_db_error_keeps_its_own_code_and_is_not_git_cant_read_the_cut()
    {
        await using var world = await BacktestWorld.Start(BacktestWorld.BeforeAllCuts);
        world.Store.Failure = ContextErrors.StoreUnavailable.With("context.db can't be opened");
        var lines = new Lines();

        var run = await world.Run("graph", configure: r => r with { Progress = lines });

        Assert.Equal(ContextErrors.StoreUnavailable.Code, run.Error.Code);
        Assert.DoesNotContain(lines.Seen, l => l.Contains("git can't read the cut", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Backtest_a_failing_adjudicator_model_stops_adjudication_marks_the_report_incomplete_and_stops_calling_it()
    {
        await using var world = await BacktestWorld.Start(BacktestWorld.BeforeAllCuts);
        await world.ImportMap();
        world.Model.Fail = _ => ContextErrors.LabelingUnavailable.With("the model is down");

        var report = (await world.Run("graph", scope: world.Human("\"eval\":{\"adjudicator\":true}"))).Value.Report;

        _ = Assert.Single(world.Model.Requests);   // one failed call, then the run stops asking
        Assert.Contains(report.Degraded, d => d.StartsWith(ContextErrors.LabelingUnavailable.Code + ": adjudication incomplete", StringComparison.Ordinal));
        Assert.All(Arm(report, "graph").Samples, s => Assert.Null(s.AdjudicatedPrecision));   // never reported as measured
    }

    private sealed class Lines : IProgress<string>
    {
        public List<string> Seen { get; } = [];

        public void Report(string value) => Seen.Add(value);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Backtest_with_a_home_inside_a_worktree_is_refused_with_005_before_any_sample_is_dropped_as_unreadable()
    {
        using var worktree = new Infrastructure.TempGitRepo(new Dictionary<string, string>(StringComparer.Ordinal) { ["a.txt"] = "a" });
        await using var world = await BacktestWorld.Start(BacktestWorld.BeforeAllCuts, homeUnder: worktree.Path);
        var lines = new Lines();

        var run = await world.Run("graph", configure: r => r with { Progress = lines });

        Assert.Equal(ContextErrors.PathInWorktree.Code, run.Error.Code);
        Assert.DoesNotContain(lines.Seen, l => l.Contains("git can't read the cut", StringComparison.Ordinal));
    }
}
