using Aw.BugFix.Core.Errors;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Features.AdvanceReproduce;

namespace Aw.BugFix.Tests.Slices;

/// <summary>A check killed by a signal, or one that left an empty log, never reached a verdict: it is neither a reproduction nor a pass.</summary>
public sealed class NoVerdictRunTests : IDisposable
{
    private readonly World _w = new();

    public void Dispose() => _w.Dispose();

    [Theory, Trait("Lane", "unit")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_baseline_killed_by_a_signal_or_with_an_empty_log_is_not_a_reproduction(bool killed)
    {
        await _w.Investigated();
        _ = killed ? _w.Runner.Killed("bash") : _w.Runner.Silent("bash");

        var run = World.Ok(await _w.TestRun());

        Assert.Equal(RunOutcome.Broken, run.Outcome);
        var error = World.Refused(await _w.Run<AdvanceReproduceRequest, AdvanceReproduceResponse>(new(World.State, run.Evidence, World.Check, World.Repo)), BugFixErrors.NotReproduced);
        Assert.Contains("empty log", error.Message, StringComparison.Ordinal);
        Assert.Contains("signal", error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_candidate_run_killed_by_a_signal_or_with_an_empty_log_is_not_recorded(bool killed)
    {
        var c1 = await _w.Candidate();
        _ = killed ? _w.Runner.Killed("bash") : _w.Runner.Silent("bash");
        var run = World.Ok(await _w.TestRun(World.Worktree));

        var error = World.Refused(await _w.Run<Features.RecordRun.RecordRunRequest, Features.RecordRun.RecordRunResponse>(new(World.State, c1, run.Evidence)), BugFixErrors.RunRefused);

        Assert.Contains("empty log", error.Message, StringComparison.Ordinal);
        Assert.Contains("signal", error.Message, StringComparison.Ordinal);
        Assert.Null(_w.Now.Candidates[0].Run);
    }
}
