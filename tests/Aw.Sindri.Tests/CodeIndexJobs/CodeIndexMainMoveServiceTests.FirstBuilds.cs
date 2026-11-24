using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Tests.CodeIndexJobs;

/// <summary>The first-builds queue (first-run spec gap 8, #3322): repos with no index build one at a time, in the profile's order, so the first map comes as early as one repo allows.</summary>
public sealed partial class CodeIndexMainMoveServiceTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task Repos_with_no_index_on_one_tick_start_one_first_build_and_the_rest_stay_queued()
    {
        _ = _rig.Heads.With(CodeIndexJobsRig.Demo, CodeIndexJobsRig.New, onDefaultBranch: true).With(CodeIndexJobsRig.Other, CodeIndexJobsRig.New, onDefaultBranch: true);

        var result = await Run();

        Assert.Equal(["demo"], [.. _start.Requests.Select(static r => r.Repo)], StringComparer.Ordinal);
        Assert.Contains("first build queued behind demo: other", result.Value.Summary, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_queued_first_build_waits_while_the_first_one_runs()
    {
        _ = _rig.Heads.With(CodeIndexJobsRig.Demo, CodeIndexJobsRig.New, onDefaultBranch: true).With(CodeIndexJobsRig.Other, CodeIndexJobsRig.New, onDefaultBranch: true);
        _rig.Job("demo", JobState.Running, TimeSpan.Zero);

        var result = await Run();

        Assert.Empty(_start.Requests);
        Assert.Contains("first build queued behind demo: other", result.Value.Summary, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_queued_first_build_starts_once_the_first_one_ended()
    {
        _ = _rig.Index.WithIndex(CodeIndexJobsRig.Demo, CodeIndexJobsRig.New);
        _ = _rig.Heads.With(CodeIndexJobsRig.Demo, CodeIndexJobsRig.New, onDefaultBranch: true).With(CodeIndexJobsRig.Other, CodeIndexJobsRig.New, onDefaultBranch: true);
        _rig.Job("demo", JobState.Succeeded, _minute, outcome: "published");

        _ = await Run();

        Assert.Equal(["other"], [.. _start.Requests.Select(static r => r.Repo)], StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_queued_first_build_starts_when_the_first_one_failed_and_backs_off()
    {
        _ = _rig.Heads.With(CodeIndexJobsRig.Demo, CodeIndexJobsRig.New, onDefaultBranch: true).With(CodeIndexJobsRig.Other, CodeIndexJobsRig.New, onDefaultBranch: true);
        _rig.Job("demo", JobState.Failed, _minute);

        _ = await Run();

        Assert.Equal(["other"], [.. _start.Requests.Select(static r => r.Repo)], StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_rebuild_after_main_moved_does_not_wait_for_a_running_first_build()
    {
        _ = _rig.Index.WithIndex(CodeIndexJobsRig.Other, CodeIndexJobsRig.Old);
        _ = _rig.Heads.With(CodeIndexJobsRig.Demo, CodeIndexJobsRig.New, onDefaultBranch: true).With(CodeIndexJobsRig.Other, CodeIndexJobsRig.New, onDefaultBranch: true);
        _rig.Job("demo", JobState.Running, TimeSpan.Zero);

        _ = await Run();

        Assert.Equal(["other"], [.. _start.Requests.Select(static r => r.Repo)], StringComparer.Ordinal);
    }
}
