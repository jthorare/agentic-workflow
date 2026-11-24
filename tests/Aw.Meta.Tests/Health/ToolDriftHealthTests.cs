using Aw.Meta.Core;
using Aw.Meta.Core.Health;
using Aw.Meta.Core.Ports;
using Aw.Meta.Features.Health;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Health;

// #4249: each installed tool against what origin/main would install, through the scripted git of the toolkit checkout /kit.
public sealed class ToolDriftHealthTests
{
    private const string Main = "aaaa000000000000000000000000000000000000";
    private const string MetaCommit = "0123456789abcdef0123456789abcdef01234567";
    private const string JudgeRev = "1111111111111111111111111111111111111111";
    private const string SindriRev = "2222222222222222222222222222222222222222";
    private const string LastMeta = "3333333333333333333333333333333333333333";
    private const string LastJudge = "4444444444444444444444444444444444444444";
    private const string LastSindri = "5555555555555555555555555555555555555555";
    private readonly MetaWorld _world = new();
    private readonly InMemoryInstalledBuilds _builds = new();
    private readonly InMemoryBuildInfo _self = new();

    private MetaPaths Paths => _world.Paths with { Heavy = "/state/bin-r1b/heavy", Reserve = "/state/bin-r1b/meta-gate-reserve", Toolkit = "/kit" };

    private ToolDriftCheck Check(MetaPaths? paths = null)
    {
        var git = new ToolkitGit(_world.Runner);
        return new ToolDriftCheck(new ScriptStaleCheck(_world.Runner, paths ?? Paths), new BuildDriftCheck(git, _builds, _self), git, _world.Values, paths ?? Paths);
    }

    private async Task<IReadOnlyList<ToolDrift>> Drifts()
    {
        var drifts = await Check().Drifts(CancellationToken.None);
        Assert.NotNull(drifts);
        return drifts;
    }

    private static ToolDrift Of(IReadOnlyList<ToolDrift> drifts, string tool) => Assert.Single(drifts, d => d.Tool == tool);

    // The scripts match, origin/main is Main, and every build is at or after origin/main's last change to its inputs. Rules added
    // later lose to these (the first match wins), so a test scripts its own answer before calling this.
    private void Box()
    {
        _self.Stamp = _self.Stamp with { Commit = MetaCommit };
        _builds.Put("judge", new InstalledBinary(JudgeRev, ["global.json", "src/Judge/Aw.Judge.Host"]));
        _builds.Put("sindri", new InstalledBinary(SindriRev, ["global.json", "src/Sindri/Aw.Sindri.Host"]));
        _ = _world.Runner.When("rev-parse origin/main:scripts/heavy.sh", stdout: "h1\n");
        _ = _world.Runner.When("hash-object /state/bin-r1b/heavy", stdout: "h1\n");
        _ = _world.Runner.When("rev-parse origin/main:scripts/meta-gate-reserve.sh", stdout: "r1\n");
        _ = _world.Runner.When("hash-object /state/bin-r1b/meta-gate-reserve", stdout: "r1\n");
        _ = _world.Runner.When("rev-parse origin/main", stdout: Main + "\n");
        _ = _world.Runner.When("show origin/main:scripts/meta-build-paths.txt", stdout: "# what install-meta.sh copies\nsrc/Meta\nscripts/install-meta.sh\n");
        _ = _world.Runner.When("log -1 --format=%H origin/main -- src/Meta scripts/install-meta.sh", stdout: LastMeta + "\n");
        _ = _world.Runner.When("log -1 --format=%H origin/main -- global.json src/Judge/Aw.Judge.Host", stdout: LastJudge + "\n");
        _ = _world.Runner.When("log -1 --format=%H origin/main -- global.json src/Sindri/Aw.Sindri.Host", stdout: LastSindri + "\n");
        _ = _world.Runner.When("merge-base --is-ancestor", exitCode: 0);
    }

    private int Count(string text) => _world.Runner.Lines.Count(l => l.Contains(text, StringComparison.Ordinal));

    [Fact, Trait("Lane", "unit")]
    public async Task A_box_built_from_origin_main_has_every_tool_current()
    {
        Box();

        var drifts = await Drifts();

        Assert.Equal(["heavy", "meta-gate-reserve", "aw-meta", "judge", "sindri"], drifts.Select(d => d.Tool));
        Assert.All(drifts, d => Assert.Equal(DriftState.Current, d.State));
        Assert.Contains($"git -C /kit merge-base --is-ancestor {LastJudge} {JudgeRev}", _world.Runner.Lines);
        Assert.Contains($"git -C /kit merge-base --is-ancestor {LastMeta} {MetaCommit}", _world.Runner.Lines);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_build_older_than_origin_mains_last_change_to_its_inputs_is_stale_with_both_commits_and_the_fix()
    {
        _ = _world.Runner.When($"merge-base --is-ancestor {LastSindri} {SindriRev}", exitCode: 1);
        Box();

        var sindri = Of(await Drifts(), "sindri");

        Assert.Equal(DriftState.Stale, sindri.State);
        Assert.Contains(SindriRev[..12], sindri.Detail, StringComparison.Ordinal);
        Assert.Contains(LastSindri[..12], sindri.Detail, StringComparison.Ordinal);
        Assert.Contains("scripts/install-cs.sh --publish --link", sindri.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", sindri.Line(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_stale_aw_meta_names_install_meta_as_the_fix()
    {
        _ = _world.Runner.When($"merge-base --is-ancestor {LastMeta} {MetaCommit}", exitCode: 1);
        Box();

        var meta = Of(await Drifts(), "aw-meta");

        Assert.Equal(DriftState.Stale, meta.State);
        Assert.Contains("scripts/install-meta.sh", meta.Detail, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_build_from_uncommitted_changes_is_stale_without_asking_git()
    {
        Box();
        _builds.Put("judge", new InstalledBinary(JudgeRev + "-dirty", ["src/Judge/Aw.Judge.Host"]));

        var judge = Of(await Drifts(), "judge");

        Assert.Equal(DriftState.Stale, judge.State);
        Assert.Contains("uncommitted", judge.Detail, StringComparison.Ordinal);
        Assert.Equal(0, Count("src/Judge/Aw.Judge.Host"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_binary_no_build_records_is_unknown_with_the_fix()
    {
        Box();
        var builds = new InMemoryInstalledBuilds();
        builds.Put("sindri", new InstalledBinary(SindriRev, ["global.json", "src/Sindri/Aw.Sindri.Host"]));
        var git = new ToolkitGit(_world.Runner);
        var check = new ToolDriftCheck(new ScriptStaleCheck(_world.Runner, Paths), new BuildDriftCheck(git, builds, _self), git, _world.Values, Paths);

        var drifts = await check.Drifts(CancellationToken.None);
        Assert.NotNull(drifts);
        var judge = Of(drifts, "judge");

        Assert.Equal(DriftState.Unknown, judge.State);
        Assert.Contains("scripts/install-cs.sh --publish --link", judge.Detail, StringComparison.Ordinal);
    }

    // Every box shows this until its next install-cs, so the line carries the fix.
    [Fact, Trait("Lane", "unit")]
    public async Task A_build_made_before_the_pathspecs_record_is_unknown_and_says_how_to_record_it()
    {
        Box();
        _builds.Put("sindri", new InstalledBinary(SindriRev, Inputs: null));

        var sindri = Of(await Drifts(), "sindri");

        Assert.Equal(DriftState.Unknown, sindri.State);
        Assert.Contains("inputs/sindri.paths", sindri.Detail, StringComparison.Ordinal);
        Assert.Contains("fix: scripts/install-cs.sh --publish --link", sindri.Detail, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_origin_main_the_builds_are_unknown_never_current()
    {
        _ = _world.Runner.When("rev-parse origin/main:", stdout: "h1\n");
        _ = _world.Runner.When("rev-parse origin/main", exitCode: 128);
        Box();

        var drifts = await Drifts();

        foreach (var tool in new[] { "aw-meta", "judge", "sindri" })
        {
            var drift = Of(drifts, tool);
            Assert.Equal(DriftState.Unknown, drift.State);
            Assert.Contains("git -C /kit fetch origin", drift.Detail, StringComparison.Ordinal);
        }

        Assert.Equal(0, Count("merge-base"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_build_commit_the_checkout_does_not_have_is_unknown()
    {
        _ = _world.Runner.When($"merge-base --is-ancestor {LastJudge} {JudgeRev}", exitCode: 128);
        Box();

        Assert.Equal(DriftState.Unknown, Of(await Drifts(), "judge").State);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_mixed_install_reports_each_tool_on_its_own()
    {
        _ = _world.Runner.When($"merge-base --is-ancestor {LastSindri} {SindriRev}", exitCode: 1);
        _ = _world.Runner.When("hash-object /state/bin-r1b/heavy", stdout: "h-old\n");
        Box();
        _self.Stamp = _self.Stamp with { Commit = "unknown" };

        var drifts = await Drifts();

        Assert.Equal(DriftState.Stale, Of(drifts, "heavy").State);
        Assert.Equal(DriftState.Current, Of(drifts, "meta-gate-reserve").State);
        Assert.Equal(DriftState.Unknown, Of(drifts, "aw-meta").State);
        Assert.Equal(DriftState.Current, Of(drifts, "judge").State);
        Assert.Equal(DriftState.Stale, Of(drifts, "sindri").State);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_second_poll_with_nothing_changed_runs_no_git_log_or_merge_base()
    {
        Box();
        var first = await Drifts();
        var logs = Count("log -1");
        var merges = Count("merge-base");

        var second = await Drifts();

        Assert.Equal(first, second);
        Assert.Equal(logs, Count("log -1"));
        Assert.Equal(merges, Count("merge-base"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_new_build_is_compared_again()
    {
        Box();
        _ = await Drifts();
        var logs = Count("log -1");
        _builds.Put("judge", new InstalledBinary(LastJudge, ["global.json", "src/Judge/Aw.Judge.Host"]));

        _ = await Drifts();

        Assert.True(Count("log -1") > logs);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_a_toolkit_checkout_nothing_is_compared()
    {
        Box();

        var drifts = await Check(Paths with { Toolkit = null }).Drifts(CancellationToken.None);

        Assert.Null(drifts);
        Assert.Empty(_world.Runner.Lines);
    }
}
