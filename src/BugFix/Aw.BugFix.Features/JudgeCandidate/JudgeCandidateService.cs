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
internal sealed class JudgeCandidateService(IStateStore store, IRunLedger ledger, IFiles files, IGit git, IJudge judge, TimeProvider clock)
    : IService<JudgeCandidateRequest, JudgeCandidateResponse>
{
    private const string NoReasons = "no-reasons";

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
        return JudgeDiff(state.Ticket, candidate, baseline.Commit).Map(diff => new ResolutionCheckInput(
            state.Ticket.Brief, state.Ticket.Expected, state.Ticket.Actual, state.Investigation?.RootCause ?? string.Empty, check.Kind, summary,
            BeforePassed: false, AfterPassed: true, diff.Stat, Capped(diff.Full), CheckSource(candidate, check, baseline.Commit), BaselineFailure(baseline.Evidence)));
    }

    // The frozen check's own file at the candidate's commit: a red commit that already holds the test is not "no proof". A
    // .csproj check is only the project: the judge needs the test classes its --filter-class names, as they were at the baseline.
    private string? CheckSource(Candidate candidate, FrozenCheck check, string baseline)
    {
        if (check.Path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            var classes = FilteredTypes(check.Command ?? []).Select(name => ClassSource(candidate, check.Path, name, baseline)).OfType<string>().ToList();
            if (classes.Count > 0)
            {
                return Capped(string.Join("\n\n", classes));
            }
        }

        var source = git.FileAt(candidate.Worktree, candidate.Commit, check.Path);
        return source.IsSuccess ? Capped(source.Value) : null;
    }

    // The simple names of the test classes the command filters on: the last dotted part of each --filter-class value ("*.BugTests"
    // names BugTests), and for --filter-method the class before the method. A name with a wildcard left in it, or anything that
    // isn't an identifier, is skipped: it names no one class.
    private static IEnumerable<string> FilteredTypes(IReadOnlyList<string> command)
    {
        var names = new List<string>();
        for (var i = 0; i < command.Count; i++)
        {
            if (command[i] is not ("--filter-class" or "--filter-method"))
            {
                continue;
            }

            for (var j = i + 1; j < command.Count && !command[j].StartsWith('-'); j++)
            {
                var dotted = command[i] == "--filter-method" ? command[j][..Math.Max(0, command[j].LastIndexOf('.'))] : command[j];
                names.Add(dotted[(dotted.LastIndexOf('.') + 1)..]);
            }
        }

        return names.Where(static n => n.Length > 0 && n.All(static c => char.IsAsciiLetterOrDigit(c) || c == '_')).Distinct(StringComparer.Ordinal);
    }

    // The files for a test class named BugTests in tests/Foo.Tests/Foo.Tests.csproj, as they were at the baseline: BugTests.cs at
    // the project's top if there is one, then every file under the project's directory that declares the type (the class is
    // usually in a subfolder). All are joined, each under a "// path" header.
    private string? ClassSource(Candidate candidate, string project, string name, string baseline)
    {
        var directory = project.Contains('/', StringComparison.Ordinal) ? project[..project.LastIndexOf('/')] : string.Empty;
        var paths = new List<string>();
        var top = (directory.Length == 0 ? string.Empty : directory + "/") + name + ".cs";
        if (git.FileAt(candidate.Worktree, baseline, top).IsSuccess)
        {
            paths.Add(top);
        }

        var declaring = git.FilesDeclaring(candidate.Worktree, baseline, directory, name);
        paths.AddRange(declaring.IsSuccess ? declaring.Value.Where(p => p != top) : []);
        var texts = paths.Select(path => git.FileAt(candidate.Worktree, baseline, path).Map(text => $"// {path}\n{text}")).Where(static r => r.IsSuccess).Select(static r => r.Value).ToList();
        return texts.Count == 0 ? null : string.Join("\n\n", texts);
    }

    // What the baseline run printed on the unfixed code: its failure lines, capped. Null when there are none or the log can't be read.
    private string? BaselineFailure(string evidence)
    {
        var log = ledger.Read(evidence).Bind(run => files.ReadText(run.Record.Output));
        if (log.IsError)
        {
            return null;
        }

        var lines = log.Value.Split('\n').Where(static l => _failureWords.Any(w => l.Contains(w, StringComparison.OrdinalIgnoreCase))).Select(static l => l.TrimEnd('\r'));
        var text = string.Join('\n', lines);
        return text.Length == 0 ? null : text.Length <= ResolutionCheckInput.FailureCap ? text : text[..ResolutionCheckInput.FailureCap] + "\n[truncated]";
    }

    private static readonly string[] _failureWords = ["fail", "error", "exception", "assert", "expected", "not ok", "panic"];

    // The candidate's own change (from the net base, so a main merge's unrelated changes stay out), after the changes of the
    // commits main already had for this GitHub issue ("Part of #N" / "Fixes #N" in the message): a fix a merged "Part of" PR
    // made is still the fix. Another kind of ticket gets the candidate's change alone.
    private Result<CommitDiff> JudgeDiff(Ticket ticket, Candidate candidate, string baseline)
    {
        var own = git.Diff(candidate.Worktree, git.NetBase(candidate.Worktree, baseline, candidate.Commit), candidate.Commit);
        if (own.IsError || GithubIssue.Number(ticket) is not { } issue)
        {
            return own;
        }

        var already = git.MainCommits(candidate.Worktree, baseline, candidate.Commit);
        var named = already.IsSuccess ? already.Value.Where(commit => GithubIssue.Names(commit.Message, issue)).ToList() : [];
        var diffs = new List<CommitDiff>();
        foreach (var commit in named)
        {
            var diff = git.Diff(candidate.Worktree, commit.Parent, commit.Sha);
            if (diff.IsError)
            {
                return diff.Error;
            }

            diffs.Add(diff.Value);
        }

        return diffs.Count == 0 ? own : Joined([.. diffs, own.Value]);
    }

    private static CommitDiff Joined(IReadOnlyList<CommitDiff> parts) =>
        new(string.Join('\n', parts.Select(static p => p.Stat)), parts.Any(static p => p.Full is null) ? null : string.Concat(parts.Select(static p => p.Full)));

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

        // A partial or unresolved answer that names nothing gives the implementer nothing to act on: treated as an escalation.
        if (answer.Decision != JudgeDecision.Resolved && answer.Reasons.All(string.IsNullOrWhiteSpace))
        {
            return Escalated(request, fresh.Value, candidate.Value, answer with { ReasonCode = NoReasons });
        }

        var status = answer.Decision == JudgeDecision.Resolved ? BugFixStatus.Active : fresh.Value.Status;   // a resolving candidate supersedes an earlier escalation in the attempt
        var verdict = new JudgeVerdict(answer.Decision, answer.DecisionId, answer.ReasonCode, answer.InputSha256, answer.Reasons);
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
        var verdict = new JudgeVerdict(JudgeDecision.Unresolved, DecisionId: null, code, answer.InputSha256, answer.Reasons);
        var fallen = state.With(candidate with { Verdict = verdict, JudgeAsked = null, JudgeEscalations = escalations });
        store.Save(request.StateDirectory, fallen.Step(Phase.Evaluate, $"judge {request.Candidate.Value} escalated twice, unresolved", clock.GetUtcNow()));
        return new JudgeCandidateResponse(request.Candidate, JudgeDecision.Unresolved, DecisionId: null, code, answer.Reasons, state.Status);
    }
}
