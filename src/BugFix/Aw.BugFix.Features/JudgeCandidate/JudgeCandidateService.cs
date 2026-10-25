using System.Globalization;
using Aw.BugFix.Core.Errors;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.BugFix.Features.JudgeCandidate;

/// <summary>
/// Asks judge resolution-check exactly once per candidate, on an input built from state (the ticket verbatim, the
/// snapshotted root cause, a description of the frozen check, the diff), and records that verdict. The orchestrator never
/// picks among verdicts, so one can't be re-rolled.
/// </summary>
internal sealed class JudgeCandidateService(IStateStore store, IRunLedger ledger, IGit git, IJudge judge, TimeProvider clock)
    : IService<JudgeCandidateRequest, JudgeCandidateResponse>
{
    public ValueTask<Result<JudgeCandidateResponse>> Handle(JudgeCandidateRequest request, CancellationToken cancellationToken)
    {
        var question = store.Locked(request.StateDirectory, () => Ask(request));
        if (question.IsError)
        {
            return ValueTask.FromResult(Results.Failure<JudgeCandidateResponse>(question.Error));
        }

        // The call can take minutes, so it runs without the lock; the candidate is marked as asked, so an interrupted
        // call can't be asked again.
        var answer = judge.ResolutionCheck(question.Value).Bind(a => SameInput(question.Value, a));
        if (answer.IsError)
        {
            _ = store.Locked(request.StateDirectory, () => Unmark(request, question.Value));   // no verdict: asking again isn't a re-roll
            return ValueTask.FromResult(Results.Failure<JudgeCandidateResponse>(answer.Error));
        }

        return ValueTask.FromResult(store.Locked(request.StateDirectory, () => Record(request, question.Value, answer.Value)));
    }

    private Result<JudgeQuestion> Ask(JudgeCandidateRequest request)
    {
        var state = store.Load(request.StateDirectory);
        var candidate = state.Bind(s => s.Require(Phase.Evaluate)).Bind(_ => state.Value.Find(request.Candidate)).Bind(static c => c.Judgeable());
        var question = candidate.Bind(c => Input(state.Value, c)).Map(input => ledger.WriteJudgeInput(request.StateDirectory, request.Candidate, input));
        if (question.IsError)
        {
            return question.Error;
        }

        store.Save(request.StateDirectory, state.Value.With(candidate.Value with { JudgeAsked = question.Value.InputSha256, JudgeAsks = candidate.Value.JudgeAsks + 1 }));
        return question;
    }

    private Result<Unit> Unmark(JudgeCandidateRequest request, JudgeQuestion question)
    {
        var state = store.Load(request.StateDirectory);
        var candidate = state.Bind(s => s.Find(request.Candidate));
        if (candidate.IsSuccess && candidate.Value.JudgeAsked == question.InputSha256)
        {
            store.Save(request.StateDirectory, state.Value.With(candidate.Value with { JudgeAsked = null, JudgeAsks = Math.Max(0, candidate.Value.JudgeAsks - 1) }));
        }

        return Results.Ok();
    }

    // Built from state, never from agent-written text, so it can't steer the verdict.
    private Result<ResolutionCheckInput> Input(BugFixState state, Candidate candidate)
    {
        var (check, baseline) = state.Frozen;
        var summary = check.Kind == CheckKind.Test
            ? $"the regression test {check.Path}, run as `{string.Join(' ', check.Command ?? [])}`"
            : $"the ui-evidence script {check.Path}";
        return git.Diff(candidate.Worktree, git.NetBase(candidate.Worktree, baseline.Commit, candidate.Commit), candidate.Commit).Map(diff => new ResolutionCheckInput(
            state.Ticket.Brief, state.Ticket.Expected, state.Ticket.Actual, state.Investigation?.RootCause ?? string.Empty, check.Kind, summary,
            BeforePassed: false, AfterPassed: true, diff.Stat, Capped(diff.Full)));
    }

    private static string? Capped(string? diff) =>
        diff is null || diff.Length <= ResolutionCheckInput.DiffCap
            ? diff
            : string.Create(CultureInfo.InvariantCulture, $"{diff[..ResolutionCheckInput.DiffCap]}\n[truncated {diff.Length - ResolutionCheckInput.DiffCap} chars]");

    // A verdict counts only for the input the helper wrote.
    private static Result<JudgeAnswer> SameInput(JudgeQuestion question, JudgeAnswer answer) =>
        answer.InputSha256 == question.InputSha256
            ? answer
            : BugFixErrors.JudgeUnavailable.With($"judge answered about input {answer.InputSha256}, not the input the helper wrote ({question.InputSha256}); no verdict recorded");

    // Recorded onto the current state, and only if it still allows it: the bug fix may have moved on (reported, a new
    // attempt) while judge was deciding, and then nothing is recorded.
    private Result<JudgeCandidateResponse> Record(JudgeCandidateRequest request, JudgeQuestion question, JudgeAnswer answer)
    {
        var fresh = store.Load(request.StateDirectory);
        var candidate = fresh.Bind(state => state.Require(Phase.Evaluate)).Bind(_ => fresh.Value.Find(request.Candidate));
        if (candidate.IsError)
        {
            return candidate.Error;
        }

        if (candidate.Value.Verdict is not null || candidate.Value.JudgeAsked != question.InputSha256)
        {
            return BugFixErrors.JudgeRefused.With($"{request.Candidate.Value} was judged or asked by another call meanwhile; that call's outcome stands");
        }

        if (candidate.Value.Attempt != fresh.Value.Attempt)
        {
            return BugFixErrors.JudgeRefused.With($"{request.Candidate.Value} was left behind (start-attempt --abandon-judge) while judge was deciding; its verdict is not recorded");
        }

        if (answer.Decision == JudgeDecision.Escalated)
        {
            return Escalated(request, fresh.Value, candidate.Value, answer);
        }

        var status = answer.Decision == JudgeDecision.Resolved ? BugFixStatus.Active : fresh.Value.Status;   // a resolving candidate supersedes an earlier escalation in the attempt
        var verdict = new JudgeVerdict(answer.Decision, answer.DecisionId, answer.ReasonCode, answer.InputSha256);
        var state = fresh.Value.With(candidate.Value with { Verdict = verdict, JudgeAsked = null }) with { Status = status };
        store.Save(request.StateDirectory, state.Step(Phase.Evaluate, $"judge {request.Candidate.Value}", clock.GetUtcNow()));
        return new JudgeCandidateResponse(request.Candidate, answer.Decision, answer.DecisionId, answer.ReasonCode, answer.Reasons, status);
    }

    // An escalation is never recorded as a verdict. The first is asked about once more (the candidate is judgeable again);
    // the second falls back to an unresolved verdict that names the escalation, so the bug fix goes on to the next attempt
    // or to a report without a human, and can never resolve the ticket.
    private Result<JudgeCandidateResponse> Escalated(JudgeCandidateRequest request, BugFixState state, Candidate candidate, JudgeAnswer answer)
    {
        var escalations = candidate.JudgeEscalations + 1;
        if (escalations < 2)
        {
            var retry = state.With(candidate with { JudgeAsked = null, JudgeAsks = 0, JudgeEscalations = escalations });
            store.Save(request.StateDirectory, retry.Step(Phase.Evaluate, $"judge {request.Candidate.Value} escalated", clock.GetUtcNow()));
            return new JudgeCandidateResponse(request.Candidate, JudgeDecision.Escalated, DecisionId: null, answer.ReasonCode, answer.Reasons, state.Status);
        }

        var code = $"escalated:{answer.ReasonCode}";
        var verdict = new JudgeVerdict(JudgeDecision.Unresolved, DecisionId: null, code, answer.InputSha256);
        var fallen = state.With(candidate with { Verdict = verdict, JudgeAsked = null, JudgeEscalations = escalations });
        store.Save(request.StateDirectory, fallen.Step(Phase.Evaluate, $"judge {request.Candidate.Value} escalated twice, unresolved", clock.GetUtcNow()));
        return new JudgeCandidateResponse(request.Candidate, JudgeDecision.Unresolved, DecisionId: null, code, answer.Reasons, state.Status);
    }
}
