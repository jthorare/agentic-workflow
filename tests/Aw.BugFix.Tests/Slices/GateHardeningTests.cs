using Aw.BugFix.Core.Errors;
using Aw.BugFix.Core.Ids;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Features.AdvanceReproduce;
using Aw.BugFix.Features.Init;
using Aw.BugFix.Features.JudgeCandidate;
using Aw.BugFix.Features.Resume;
using Aw.BugFix.Features.RunTest;
using Aw.BugFix.Features.StartAttempt;
using Aw.BugFix.Testing;
using Aw.Kernel;

namespace Aw.BugFix.Tests.Slices;

/// <summary>The gates an agent could try to slip past to call a bug resolved without fixing it.</summary>
public sealed class GateHardeningTests : IDisposable
{
    private readonly World _w = new();

    public void Dispose() => _w.Dispose();

    [Fact, Trait("Lane", "unit")]
    public async Task A_same_named_file_elsewhere_does_not_count_as_the_check()
    {
        await _w.Investigated();
        _w.Repos.Write("/repo/lib/tests/bug.test.sh", "exit 0\n");
        _ = _w.Repos.Commit(World.Repo);

        var result = await TestRun("/repo/lib", "../tests/bug.test.sh", ["bash", "tests/bug.test.sh"]);

        Assert.Contains("resolves to it", World.Refused(result, BugFixErrors.CommandRefused).Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_baseline_that_did_not_run_is_not_a_reproduction(bool hangs)
    {
        await _w.Investigated();
        _ = hangs ? _w.Runner.Hangs("pytest") : _w.Runner;   // unknown programs can't start in the fake runner

        var run = World.Ok(await TestRun(World.Repo, World.Check, ["pytest", World.Check]));

        Assert.Equal(RunOutcome.Broken, run.Outcome);
        Assert.Contains("didn't start, timed out", World.Refused(await Reproduce(run.Evidence), BugFixErrors.NotReproduced).Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_fix_that_edits_the_frozen_commands_runner_needs_approval()
    {
        _w.Repos.Write("/repo/scripts/run.sh", "bash \"$1\"\n");
        _ = _w.Repos.Commit(World.Repo);
        await _w.Investigated();
        var run = World.Ok(await TestRun(World.Repo, World.Check, ["bash", "scripts/run.sh", World.Check]));
        _ = World.Ok(await Reproduce(run.Evidence));
        Assert.Equal(["scripts/run.sh"], _w.Now.Check?.CommandFiles);
        _ = World.Ok(await _w.Start());
        _ = _w.Fix(changes: new(StringComparer.Ordinal) { ["src/app.ts"] = "fixed\n", ["scripts/run.sh"] = "exit 0\n" });

        Assert.Contains("scripts/run.sh", World.Refused(await _w.Record(), BugFixErrors.CandidateRefused).Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Mode_B_points_at_its_second_candidate_but_can_move_on()
    {
        var c1 = await _w.Candidate();
        _ = World.Ok(await _w.RunAgainst(c1, exitCode: 1));
        _ = World.Ok(await _w.Start(FixMode.B));
        _ = _w.Fix("h1", "/wt/h1", new(StringComparer.Ordinal) { ["src/app.ts"] = "h1\n" });
        var c2 = World.Ok(await _w.Record("h1", "/wt/h1", hypothesis: 1)).Candidate;
        _ = _w.Runner.Exits("bash", 1);
        var run = World.Ok(await _w.TestRun("/wt/h1"));
        _ = World.Ok(await _w.Run<Features.RecordRun.RecordRunRequest, Features.RecordRun.RecordRunResponse>(new(World.State, c2, run.Evidence)));

        Assert.StartsWith("record-candidate for the second open hypothesis", World.Ok(await Resume()).Next, StringComparison.Ordinal);
        Assert.Equal(3, World.Ok(await _w.Start()).Attempt);   // the second implementer may produce nothing usable: the next attempt is still open
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_resolving_candidate_wins_over_an_escalated_one_in_the_same_attempt()
    {
        var c1 = await _w.Candidate();
        _ = World.Ok(await _w.RunAgainst(c1, exitCode: 1));
        _ = World.Ok(await _w.Start(FixMode.B));
        _ = _w.Fix("h1", "/wt/h1", new(StringComparer.Ordinal) { ["src/app.ts"] = "h1\n" });
        _ = _w.Fix("h2", "/wt/h2", new(StringComparer.Ordinal) { ["src/app.ts"] = "h2\n" });
        var c2 = World.Ok(await _w.Record("h1", "/wt/h1", hypothesis: 1)).Candidate;
        var c3 = World.Ok(await _w.Record("h2", "/wt/h2", hypothesis: 2)).Candidate;
        await PassAndJudge(c2, "/wt/h1", JudgeDecision.Escalated);
        Assert.Null(_w.Now.Candidates[1].Verdict);   // an escalation records nothing
        Assert.Equal(BugFixStatus.Active, _w.Now.Status);

        await PassAndJudge(c3, "/wt/h2", JudgeDecision.Resolved);

        Assert.Equal(BugFixStatus.Active, _w.Now.Status);
        Assert.Equal("advance report --candidate c3", World.Ok(await Resume()).Next);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_verdict_about_another_input_is_not_recorded()
    {
        var c1 = await _w.Candidate();
        _ = World.Ok(await _w.RunAgainst(c1));
        _ = _w.Judge.Answers(A.Verdict(JudgeDecision.Resolved, aboutInput: new string('0', 64)));

        Assert.Contains("not the input the helper wrote", World.Refused(await Judge(c1), BugFixErrors.JudgeUnavailable).Message, StringComparison.Ordinal);
        Assert.Null(_w.Now.Candidates[0].Verdict);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_step_waits_for_no_one_while_another_call_holds_the_state()
    {
        _ = _w.Store.Held.Add(World.State);

        _ = World.Refused(await _w.Run<InitRequest, InitResponse>(new InitRequest(World.State, World.TicketFile)), BugFixErrors.StateBusy);
        Assert.Equal(0, _w.Store.Saves);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_verdict_that_arrives_after_the_report_is_not_recorded()
    {
        var c1 = await _w.Candidate();
        _ = World.Ok(await _w.RunAgainst(c1));
        _w.Judge.WhileDeciding = () => World.Ok(_w.Run<Features.AdvanceReport.AdvanceReportRequest, Features.AdvanceReport.AdvanceReportResponse>(
            new(World.State, Candidate: null, Unresolved: true)).GetAwaiter().GetResult());

        _ = World.Refused(await Judge(c1), BugFixErrors.WrongPhase);

        Assert.Equal((Phase.Report, BugFixStatus.Unresolved), (_w.Now.Phase, _w.Now.Status));
        Assert.Null(_w.Now.Candidates[0].Verdict);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_interrupted_judge_call_is_retried_once_and_then_only_abandoned()
    {
        var c1 = await _w.Candidate();
        _ = World.Ok(await _w.RunAgainst(c1));
        _w.Judge.WhileDeciding = static () => throw new OperationCanceledException("killed while judge was deciding");

        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => Judge(c1));
        Assert.StartsWith("judge c1 again", World.Ok(await Resume()).Next, StringComparison.Ordinal);   // one automatic retry
        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => Judge(c1));
        _w.Judge.WhileDeciding = null;

        Assert.Contains("no answer was recorded", World.Refused(await Judge(c1), BugFixErrors.JudgeRefused).Message, StringComparison.Ordinal);
        Assert.Equal(2, _w.Judge.Asked.Count);
        var next = World.Ok(await Resume()).Next;
        Assert.Contains("--abandon-judge", next, StringComparison.Ordinal);
        Assert.DoesNotContain("ask the user", next, StringComparison.Ordinal);
        Assert.Contains("--abandon-judge", World.Refused(await _w.Start(), BugFixErrors.AttemptRefused).Message, StringComparison.Ordinal);
        var moved = World.Ok(await _w.Run<StartAttemptRequest, StartAttemptResponse>(new StartAttemptRequest(World.State, FixMode.A, AbandonJudge: true)));
        Assert.Equal(2, moved.Attempt);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_judge_call_can_be_asked_again()
    {
        var c1 = await _w.Candidate();
        _ = World.Ok(await _w.RunAgainst(c1));
        _ = _w.Judge.Answers(ScriptedJudge.Fails());

        _ = World.Refused(await Judge(c1), BugFixErrors.JudgeUnavailable);

        Assert.Null(_w.Now.Candidates[0].JudgeAsked);
        Assert.Equal(JudgeDecision.Resolved, World.Ok(await Judge(c1)).Decision);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_run_whose_program_resolved_elsewhere_is_not_the_frozen_check()
    {
        var c1 = await _w.Candidate();
        _ = _w.Runner.Resolves("bash", "a bash someone put first on PATH");

        Assert.Contains("resolved to a different file", World.Refused(await _w.RunAgainst(c1), BugFixErrors.RunRefused).Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Files_named_in_flag_values_are_protected_and_a_missing_one_refuses_the_baseline()
    {
        _w.Repos.Write("/repo/lib/setup.cjs", "hook\n");
        _ = _w.Repos.Commit(World.Repo);
        await _w.Investigated();
        _ = _w.Runner.Exits("node", 1);
        var missing = World.Ok(await TestRun(World.Repo, World.Check, ["node", "--config=tools/vt.ts", World.Check]));
        Assert.Contains("tools/vt.ts", World.Refused(await Reproduce(missing.Evidence), BugFixErrors.CheckNotFrozen).Message, StringComparison.Ordinal);

        var run = World.Ok(await TestRun(World.Repo, World.Check, ["node", "--require=./lib/setup.cjs", World.Check]));
        _ = World.Ok(await Reproduce(run.Evidence));

        Assert.Equal(["lib/setup.cjs"], _w.Now.Check?.CommandFiles);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_run_in_another_environment_is_not_the_frozen_check()
    {
        var c1 = await _w.Candidate();
        _ = _w.Runner.RunsWithPath("/tmp/planted:/usr/bin:/bin");

        Assert.Contains("different PATH", World.Refused(await _w.RunAgainst(c1), BugFixErrors.RunRefused).Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_module_named_without_its_extension_is_protected()
    {
        _w.Repos.Write("/repo/lib/setup.js", "hook\n");
        _ = _w.Repos.Commit(World.Repo);
        await _w.Investigated();
        _ = _w.Runner.Exits("node", 1);

        var run = World.Ok(await TestRun(World.Repo, World.Check, ["node", "--require=./lib/setup", World.Check]));
        _ = World.Ok(await Reproduce(run.Evidence));

        Assert.Contains("lib/setup.js", _w.Now.Check?.CommandFiles ?? [], StringComparer.Ordinal);
        Assert.Contains("lib/setup", _w.Now.Check?.CommandFiles ?? [], StringComparer.Ordinal);   // a file the loader would try first, though it doesn't exist
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_new_file_that_shadows_a_python_module_is_protected()
    {
        await _w.Investigated();
        _ = _w.Runner.Exits("python3", 1);
        var run = World.Ok(await TestRun(World.Repo, World.Check, ["python3", "-m", "unittest", World.Check]));
        _ = World.Ok(await Reproduce(run.Evidence));
        _ = World.Ok(await _w.Start());
        _ = _w.Fix(changes: new(StringComparer.Ordinal) { ["src/app.ts"] = "fixed\n", ["unittest.py"] = "raise SystemExit(0)\n" });

        Assert.Contains("unittest.py", World.Refused(await _w.Record(), BugFixErrors.CandidateRefused).Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Option_values_that_are_not_files_are_neither_required_nor_protected()
    {
        _w.Repos.Write("/repo/app/__init__.py", "x\n");
        _ = _w.Repos.Commit(World.Repo);
        await _w.Investigated();
        _ = _w.Runner.Exits("pytest", 1);
        var run = World.Ok(await TestRun(World.Repo, World.Check, ["pytest", "--cov=app", "--junitxml=out/report.xml", "-k", "/login/", World.Check]));

        _ = World.Ok(await Reproduce(run.Evidence));

        Assert.Empty(_w.Now.Check?.CommandFiles ?? ["?"]);   // the fix may change app/__init__.py, the code under test
    }

    private async Task PassAndJudge(CandidateId candidate, string worktree, JudgeDecision decision)
    {
        _ = _w.Runner.Exits("bash", 0);
        var run = World.Ok(await _w.TestRun(worktree));
        _ = World.Ok(await _w.Run<Features.RecordRun.RecordRunRequest, Features.RecordRun.RecordRunResponse>(new(World.State, candidate, run.Evidence)));
        _ = _w.Judge.Answers(A.Verdict(decision));
        _ = World.Ok(await Judge(candidate));
    }

    private Task<Result<RunTestResponse>> TestRun(string directory, string check, string[] command) =>
        _w.Run<RunTestRequest, RunTestResponse>(new RunTestRequest(World.State, check, directory, command));

    private Task<Result<AdvanceReproduceResponse>> Reproduce(string evidence) =>
        _w.Run<AdvanceReproduceRequest, AdvanceReproduceResponse>(new AdvanceReproduceRequest(World.State, evidence, World.Check, World.Repo));

    private Task<Result<JudgeCandidateResponse>> Judge(CandidateId candidate) =>
        _w.Run<JudgeCandidateRequest, JudgeCandidateResponse>(new JudgeCandidateRequest(World.State, candidate));

    private Task<Result<ResumeResponse>> Resume() => _w.Run<ResumeRequest, ResumeResponse>(new ResumeRequest(World.State));
}
