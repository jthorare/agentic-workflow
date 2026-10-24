using Aw.BugFix.Core.Errors;
using Aw.BugFix.Core.Ids;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Features.AdvanceReport;
using Aw.BugFix.Features.JudgeCandidate;
using Aw.BugFix.Features.RecordRun;
using Aw.BugFix.Features.Resume;
using Aw.BugFix.Features.Status;
using Aw.BugFix.Testing;
using Aw.Kernel;

namespace Aw.BugFix.Tests.Slices;

public sealed class EvaluateTests : IDisposable
{
    private readonly World _w = new();

    public void Dispose() => _w.Dispose();

    [Fact, Trait("Lane", "unit")]
    public async Task Record_run_records_a_passing_run_and_points_at_judge()
    {
        var c1 = await _w.Candidate();

        var result = World.Ok(await _w.RunAgainst(c1));

        Assert.Equal((c1, true, "judge c1"), (result.Candidate, result.Passed, result.Next));
        Assert.Equal(Phase.Evaluate, _w.Now.Phase);
        Assert.True(_w.Now.Candidates[0].Run?.Passed);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Record_run_refuses_a_second_run_a_run_at_another_commit_and_an_unknown_candidate()
    {
        var c1 = await _w.Candidate();
        _ = _w.Runner.Exits("bash", 0);
        var atBaseline = World.Ok(await _w.TestRun());

        Assert.Contains("not c1's commit", World.Refused(await Record(c1, atBaseline.Evidence), BugFixErrors.RunRefused).Message, StringComparison.Ordinal);
        _ = World.Ok(await _w.RunAgainst(c1));
        _ = World.Refused(await _w.RunAgainst(c1), BugFixErrors.RunRefused);
        _ = World.Refused(await Record(CandidateId.From("c9"), atBaseline.Evidence), BugFixErrors.UnknownCandidate);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Record_run_refuses_a_run_of_another_command_than_the_baseline()
    {
        var c1 = await _w.Candidate();
        _ = _w.Runner.Exits("sh", 0);
        var run = World.Ok(await _w.Run<Features.RunTest.RunTestRequest, Features.RunTest.RunTestResponse>(
            new Features.RunTest.RunTestRequest(World.State, World.Check, World.Worktree, ["sh", World.Check])));

        var error = World.Refused(await Record(c1, run.Evidence), BugFixErrors.RunRefused);

        Assert.Contains("differs from the baseline's", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Record_run_refuses_when_the_candidate_branch_moved_since_it_was_recorded()
    {
        var c1 = await _w.Candidate();
        _ = _w.Runner.Exits("bash", 0);
        var run = World.Ok(await _w.TestRun(World.Worktree));
        _w.Repos.Write($"{World.Worktree}/src/app.ts", "more\n");
        _ = _w.Repos.Commit(World.Worktree);

        _ = World.Refused(await Record(c1, run.Evidence), BugFixErrors.RunRefused);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Judge_records_the_verdict_on_the_input_built_from_state()
    {
        var c1 = await _w.Candidate();
        _ = World.Ok(await _w.RunAgainst(c1));

        var result = World.Ok(await Judge(c1));

        Assert.Equal((JudgeDecision.Resolved, "d-42", BugFixStatus.Active), (result.Decision, result.DecisionId?.Value, result.Status));
        Assert.Equal(["the reported page now opens"], result.Reasons);
        var asked = Assert.Single(_w.Judge.Asked);
        Assert.Equal(_w.Now.Ticket.Brief, asked.Brief);
        Assert.Equal("The session cookie is dropped on redirect.", asked.RootCause);
        Assert.Equal("the regression test tests/bug.test.sh, run as `bash tests/bug.test.sh`", asked.CheckSummary);
        Assert.Equal((false, true), (asked.BeforePassed, asked.AfterPassed));
        Assert.Contains("keep cookie", asked.Diff, StringComparison.Ordinal);
        Assert.Same(asked, _w.Ledger.JudgeInputs[c1]);
        Assert.Equal(JudgeDecision.Resolved, _w.Now.Candidates[0].Verdict?.Decision);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Judge_is_asked_once_per_candidate_and_only_after_a_passing_run()
    {
        var c1 = await _w.Candidate();
        _ = World.Refused(await Judge(c1), BugFixErrors.WrongPhase);
        _ = World.Ok(await _w.RunAgainst(c1));
        _ = _w.Judge.Answers(A.Verdict(JudgeDecision.Partial));
        _ = World.Ok(await Judge(c1));

        var error = World.Refused(await Judge(c1), BugFixErrors.JudgeRefused);

        Assert.Contains("a verdict is never re-asked", error.Message, StringComparison.Ordinal);
        _ = Assert.Single(_w.Judge.Asked);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Judge_refuses_a_candidate_whose_run_failed()
    {
        var c1 = await _w.Candidate();
        _ = World.Ok(await _w.RunAgainst(c1, exitCode: 1));

        var error = World.Refused(await Judge(c1), BugFixErrors.JudgeRefused);

        Assert.Contains("no passing run", error.Message, StringComparison.Ordinal);
        Assert.Empty(_w.Judge.Asked);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_judge_failure_records_nothing_so_a_retry_is_not_a_re_roll()
    {
        var c1 = await _w.Candidate();
        _ = World.Ok(await _w.RunAgainst(c1));
        _ = _w.Judge.Answers(ScriptedJudge.Fails());

        _ = World.Refused(await Judge(c1), BugFixErrors.JudgeUnavailable);
        Assert.Null(_w.Now.Candidates[0].Verdict);

        Assert.Equal(JudgeDecision.Resolved, World.Ok(await Judge(c1)).Decision);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_first_escalation_is_retried_and_never_recorded_as_a_verdict()
    {
        var c1 = await _w.Candidate();
        _ = World.Ok(await _w.RunAgainst(c1));
        _ = _w.Judge.Answers(A.Verdict(JudgeDecision.Escalated));

        var result = World.Ok(await Judge(c1));

        Assert.Equal((JudgeDecision.Escalated, BugFixStatus.Active), (result.Decision, result.Status));
        Assert.Null(_w.Now.Candidates[0].Verdict);
        Assert.StartsWith("judge c1 again", World.Ok(await Resume()).Next, StringComparison.Ordinal);
        Assert.Equal(JudgeDecision.Resolved, World.Ok(await Judge(c1)).Decision);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_second_escalation_falls_back_to_unresolved_and_the_bug_fix_continues()
    {
        var c1 = await _w.Candidate();
        _ = World.Ok(await _w.RunAgainst(c1));
        _ = _w.Judge.Answers(A.Verdict(JudgeDecision.Escalated)).Answers(A.Verdict(JudgeDecision.Escalated));
        _ = World.Ok(await Judge(c1));

        var result = World.Ok(await Judge(c1));

        Assert.Equal((JudgeDecision.Unresolved, BugFixStatus.Active), (result.Decision, result.Status));
        Assert.StartsWith("escalated", result.ReasonCode, StringComparison.Ordinal);
        Assert.DoesNotContain("ask the user", World.Ok(await Resume()).Next, StringComparison.Ordinal);
        _ = World.Refused(await Report(c1), BugFixErrors.ReportRefused);   // a fallback never resolves the ticket
        _ = World.Ok(await _w.Start());
        Assert.Equal(BugFixStatus.Active, _w.Now.Status);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_judge_input_caps_a_runaway_diff()
    {
        await _w.Reproduced();
        _ = World.Ok(await _w.Start());
        _ = _w.Fix(changes: new(StringComparer.Ordinal) { ["src/app.ts"] = new string('x', ResolutionCheckInput.DiffCap + 10) });
        var c1 = World.Ok(await _w.Record()).Candidate;
        _ = World.Ok(await _w.RunAgainst(c1));

        _ = World.Ok(await Judge(c1));

        var diff = Assert.Single(_w.Judge.Asked).Diff!; // why: the fake git always produces a diff.
        Assert.EndsWith("chars]", diff, StringComparison.Ordinal);
        Assert.True(diff.Length < ResolutionCheckInput.DiffCap + 100);
        Assert.Equal(1_000_000, ResolutionCheckInput.DiffCap);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Advance_report_resolves_only_with_a_passing_run_and_a_resolved_verdict()
    {
        var c1 = await _w.Candidate();
        Assert.Contains("run: none, judge: none", World.Refused(await Report(c1), BugFixErrors.ReportRefused).Message, StringComparison.Ordinal);
        _ = World.Ok(await _w.RunAgainst(c1));
        _ = World.Ok(await Judge(c1));
        Assert.Equal("advance report --candidate c1", World.Ok(await Resume()).Next);

        var result = World.Ok(await Report(c1));

        Assert.Equal((Phase.Report, BugFixStatus.Resolved, c1), (result.Phase, result.Status, result.Candidate));
        Assert.Equal(c1, _w.Now.ResolvedBy);
        Assert.StartsWith("done (resolved)", World.Ok(await Resume()).Next, StringComparison.Ordinal);
        _ = World.Refused(await Report(c1), BugFixErrors.ReportRefused);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Advance_report_unresolved_needs_exactly_one_outcome()
    {
        await _w.Reproduced();

        _ = World.Refused(await _w.Run<AdvanceReportRequest, AdvanceReportResponse>(new AdvanceReportRequest(World.State, CandidateId.From("c1"), Unresolved: true)), BugFixErrors.Invalid);
        _ = World.Refused(await _w.Run<AdvanceReportRequest, AdvanceReportResponse>(new AdvanceReportRequest(World.State, Candidate: null, Unresolved: false)), BugFixErrors.Invalid);
        var result = World.Ok(await _w.Run<AdvanceReportRequest, AdvanceReportResponse>(new AdvanceReportRequest(World.State, Candidate: null, Unresolved: true)));

        Assert.Equal((Phase.Report, BugFixStatus.Unresolved), (result.Phase, result.Status));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Resume_names_the_next_step_in_each_phase_and_status_shows_the_state()
    {
        _ = World.Ok(await _w.Init());
        Assert.StartsWith("run /rootCause", World.Ok(await Resume()).Next, StringComparison.Ordinal);
        var c1 = await CandidateAfterInit();

        Assert.Equal($"detach the main checkout at {_w.Now.Candidates[0].Commit} (c1), run the same check, then record-run c1", World.Ok(await Resume()).Next);
        _ = World.Ok(await _w.RunAgainst(c1, exitCode: 1));
        Assert.Equal("start-attempt with the failure reasons", World.Ok(await Resume()).Next);
        var status = World.Ok(await _w.Run<StatusRequest, BugFixState>(new StatusRequest(World.State)));
        Assert.Equal(_w.Base, status.Baseline?.Commit);
    }

    private async Task<CandidateId> CandidateAfterInit()
    {
        _ = World.Ok(await _w.Run<Features.AdvanceInvestigate.AdvanceInvestigateRequest, Features.AdvanceInvestigate.AdvanceInvestigateResponse>(
            new Features.AdvanceInvestigate.AdvanceInvestigateRequest(World.State, World.HandoffFile)));
        var run = World.Ok(await _w.TestRun());
        _ = World.Ok(await _w.Run<Features.AdvanceReproduce.AdvanceReproduceRequest, Features.AdvanceReproduce.AdvanceReproduceResponse>(
            new Features.AdvanceReproduce.AdvanceReproduceRequest(World.State, run.Evidence, World.Check, World.Repo)));
        _ = World.Ok(await _w.Start());
        _ = _w.Fix();
        return World.Ok(await _w.Record()).Candidate;
    }

    private Task<Result<RecordRunResponse>> Record(CandidateId candidate, string evidence) =>
        _w.Run<RecordRunRequest, RecordRunResponse>(new RecordRunRequest(World.State, candidate, evidence));

    private Task<Result<JudgeCandidateResponse>> Judge(CandidateId candidate) =>
        _w.Run<JudgeCandidateRequest, JudgeCandidateResponse>(new JudgeCandidateRequest(World.State, candidate));

    private Task<Result<AdvanceReportResponse>> Report(CandidateId candidate) =>
        _w.Run<AdvanceReportRequest, AdvanceReportResponse>(new AdvanceReportRequest(World.State, candidate, Unresolved: false));

    private Task<Result<ResumeResponse>> Resume() => _w.Run<ResumeRequest, ResumeResponse>(new ResumeRequest(World.State));
}
