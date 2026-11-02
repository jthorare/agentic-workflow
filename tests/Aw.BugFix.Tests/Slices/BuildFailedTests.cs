using Aw.BugFix.Core.Errors;
using Aw.BugFix.Core.Ids;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Features.RecordRun;
using Aw.Kernel;

namespace Aw.BugFix.Tests.Slices;

/// <summary>A candidate that doesn't build has no check to run: record-run --build-failed marks it failed so the next attempt can start.</summary>
public sealed class BuildFailedTests : IDisposable
{
    private const string Log = "/state/build.log";

    private readonly World _w = new();

    public void Dispose() => _w.Dispose();

    [Fact, Trait("Lane", "unit")]
    public async Task Record_build_failed_marks_the_candidate_failed_and_lets_the_next_attempt_start()
    {
        var c1 = await _w.Candidate();
        _w.Repos.Write(Log, "error CS1002: ; expected\n");

        var result = World.Ok(await BuildFailed(c1));

        Assert.Equal((c1, false), (result.Candidate, result.Passed));
        Assert.Equal(Phase.Evaluate, _w.Now.Phase);
        Assert.Equal(Log, _w.Now.Candidates[0].Run?.Evidence);
        Assert.False(_w.Now.Candidates[0].Run?.Passed);
        Assert.Equal(2, World.Ok(await _w.Start()).Attempt);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData("  \n")]
    public async Task Record_build_failed_refuses_an_empty_build_log(string text)
    {
        var c1 = await _w.Candidate();
        _w.Repos.Write(Log, text);

        _ = World.Refused(await BuildFailed(c1), BugFixErrors.RunRefused);
        Assert.Null(_w.Now.Candidates[0].Run);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Record_build_failed_refuses_a_missing_log_a_recorded_run_and_an_unknown_candidate()
    {
        var c1 = await _w.Candidate();

        _ = World.Refused(await BuildFailed(c1), BugFixErrors.RunRefused);
        _w.Repos.Write(Log, "error\n");
        _ = World.Refused(await BuildFailed(CandidateId.From("c9")), BugFixErrors.UnknownCandidate);
        _ = World.Ok(await BuildFailed(c1));
        _ = World.Refused(await BuildFailed(c1), BugFixErrors.RunRefused);
    }

    private Task<Result<RecordRunResponse>> BuildFailed(CandidateId candidate) =>
        _w.Run<RecordBuildFailedRequest, RecordRunResponse>(new RecordBuildFailedRequest(World.State, candidate, Log));
}
