using Aw.Kernel.Json;
using Aw.Meta.Core.Watch;
using Aw.Meta.Host;
using Aw.Meta.Infrastructure.Config;
using Aw.Meta.Tests.Adapters;
using Aw.Meta.Tests.Cli;

namespace Aw.Meta.Tests.Watch;

/// <summary>#2771: the watch reports only the team's pull requests, and `watch --help` prints usage.</summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class WatchTeamPrsTests : IDisposable
{
    private readonly CliHarness _cli = new();

    private static PrView Pr(int number, string head, string baseBranch = "main", PrState state = PrState.Open) =>
        new(Number: number, State: state, IsDraft: false, Head: "aaaaaaa1", Mergeable: "MERGEABLE") { HeadBranch = head, BaseBranch = baseBranch };

    private static HashSet<int> None() => [];

    private static List<int> Owned(IReadOnlyList<PrView> prs, IReadOnlySet<int>? queued = null, IReadOnlySet<int>? ready = null, string[]? prefixes = null, params string[] checkedOut) =>
        [.. TeamPrs.Owned(prs, queued ?? None(), ready ?? None(), prefixes, new HashSet<string>(checkedOut, StringComparer.Ordinal)).Select(p => p.Number)];

    [Fact, Trait("Lane", "unit")]
    public void Foreign_pr_is_not_owned_when_prefix_set() =>
        Assert.Equal([1], Owned([Pr(1, "ab-cp4/y"), Pr(2, "other-team/x")], prefixes: ["ab-cp4/"]));

    [Fact, Trait("Lane", "unit")]
    public void Without_prefixes_a_pr_whose_branch_is_checked_out_in_a_team_worktree_is_owned() =>
        Assert.Equal([1], Owned([Pr(1, "bugfix/a"), Pr(2, "bugfix/b")], prefixes: null, checkedOut: "bugfix/a"));

    [Fact, Trait("Lane", "unit")]
    public void A_pr_stacked_on_an_owned_pr_is_owned_two_levels_deep() =>
        Assert.Equal([1, 2, 3], Owned([Pr(1, "ab-cp4/a"), Pr(2, "x/b", "ab-cp4/a"), Pr(3, "x/c", "x/b"), Pr(4, "x/d")], prefixes: ["ab-cp4/"]).Order().ToList());

    [Fact, Trait("Lane", "unit")]
    public void A_queued_pr_and_a_ready_sent_pr_are_owned_whatever_their_branch() =>
        Assert.Equal([1, 2], Owned([Pr(1, "x/a"), Pr(2, "x/b"), Pr(3, "x/c")], queued: new HashSet<int> { 1 }, ready: new HashSet<int> { 2 }, prefixes: ["ab-cp4/"]).Order().ToList());

    [Fact, Trait("Lane", "unit")]
    public void A_foreign_pr_merging_gives_no_pr_line_but_main_moved_still_shows()
    {
        var own = Pr(1, "ab-cp4/a");
        var foreign = Pr(2, "other/x");
        var all = new[] { own, foreign };
        var merged = new[] { own with { State = PrState.Merged }, foreign with { State = PrState.Merged } };
        var lines = new List<string>();
        var state = WatchState.Initial;
        foreach (var (prs, main) in new[] { (all, "m1"), (merged, "m2") })
        {
            var kept = TeamPrs.Owned(prs, None(), None(), ["ab-cp4/"], new HashSet<string>(StringComparer.Ordinal));
            var step = WatchDiff.Step(state, new WatchSnapshot(kept, main, DiskLow: false));
            lines.AddRange(step.Lines);
            state = step.State;
        }

        Assert.Equal(["PR #1 merged", "MAIN moved m2"], lines, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Team_json_reads_the_watch_branch_prefixes_and_defaults_to_null()
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, "team.json");
        var config = new FileMetaConfig(path, AwJson.CreateOptions(MetaJsonContext.Default));

        await File.WriteAllTextAsync(path, """{"team":"t1","repo":"/w","lanes":["meta"],"watch":{"prBranchPrefixes":["ab-cp4/"]}}""", TestContext.Current.CancellationToken);
        var with = (await config.Load(CancellationToken.None)).Value;
        await File.WriteAllTextAsync(path, """{"team":"t1","repo":"/w","lanes":["meta"]}""", TestContext.Current.CancellationToken);
        var without = (await config.Load(CancellationToken.None)).Value;

        Assert.Equal(["ab-cp4/"], with.Watch.PrBranchPrefixes);
        Assert.Null(without.Watch.PrBranchPrefixes);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Watch_help_prints_the_usage_on_stdout_and_exits_zero()
    {
        await File.WriteAllTextAsync(_cli.File("team.json"), """{"team":"t","repo":"/nonexistent","lanes":["api"],"metaLane":"meta"}""", TestContext.Current.CancellationToken);
        _cli.SetEnv("AW_META_CONFIG", _cli.File("team.json"));

        var result = await _cli.Run("watch", "--help");

        Assert.Equal(0, result.Exit);
        Assert.Contains("--all-prs", result.Out, StringComparison.Ordinal);
    }

    public void Dispose() => _cli.Dispose();
}
