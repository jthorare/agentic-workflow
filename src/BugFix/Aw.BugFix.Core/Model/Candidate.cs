using Aw.BugFix.Core.Errors;
using Aw.BugFix.Core.Ids;
using Aw.Kernel;

namespace Aw.BugFix.Core.Model;

/// <summary>A fix candidate: a commit on top of the baseline, its check run and its judge verdict.</summary>
/// <param name="Id">c1, c2, … in recording order.</param>
/// <param name="Attempt">The attempt it belongs to (1-based).</param>
/// <param name="Mode">The attempt's mode.</param>
/// <param name="Branch">The worktree's branch.</param>
/// <param name="Worktree">The worktree's full path.</param>
/// <param name="Commit">The candidate commit.</param>
/// <param name="Hypothesis">The hypothesis number it pursues (required in mode B).</param>
/// <param name="ChangedFiles">Files changed since the baseline, as recorded.</param>
/// <param name="Run">The frozen check's run against it, once recorded.</param>
/// <param name="Verdict">judge's verdict, once recorded.</param>
/// <param name="JudgeAsked">The SHA-256 of the input judge was asked about and hasn't answered yet: set before the call, so an interrupted call is noticed.</param>
/// <param name="JudgeAsks">How many times judge was asked while no answer was recorded: an interrupted call is retried once.</param>
/// <param name="JudgeEscalations">How many times judge escalated about this candidate: the first is retried, the second falls back to unresolved.</param>
public sealed record Candidate(
    CandidateId Id,
    int Attempt,
    FixMode Mode,
    string Branch,
    string Worktree,
    string Commit,
    int? Hypothesis,
    IReadOnlyList<string> ChangedFiles,
    CandidateRun? Run,
    JudgeVerdict? Verdict,
    string? JudgeAsked = null,
    int JudgeAsks = 0,
    int JudgeEscalations = 0)
{
    /// <summary>How many times judge may be asked while no answer was recorded: the call and one retry after an interruption.</summary>
    public const int MaxJudgeAsks = 2;

    /// <summary>Gets a value indicating whether the candidate has a passing run and a resolved verdict.</summary>
    public bool Resolves => Run is { Passed: true } && Verdict?.Decision == JudgeDecision.Resolved;

    /// <summary>Whether judge may be asked about this candidate: once, and only after a passing run.</summary>
    /// <returns>The candidate, or AW-BUGFIX-015.</returns>
    public Result<Candidate> Judgeable()
    {
        if (Run is not { Passed: true })
        {
            return BugFixErrors.JudgeRefused.With($"{Id.Value} has no passing run; judge only runs after the check passes");
        }

        if (Verdict is not null)
        {
            return BugFixErrors.JudgeRefused.With($"{Id.Value} was already judged ({Names.Of(Verdict.Decision)}); a verdict is never re-asked");
        }

        return JudgeAsked is null || JudgeAsks < MaxJudgeAsks
            ? this
            : BugFixErrors.JudgeRefused.With($"judge was asked about {Id.Value} {MaxJudgeAsks} times and no answer was recorded; if no judge process is still running, leave it with start-attempt --abandon-judge, or run advance report --unresolved");
    }

    /// <summary>Whether the bug fix may be reported resolved by this candidate: only with a passing run and a resolved verdict.</summary>
    /// <returns>The candidate, or AW-BUGFIX-019.</returns>
    public Result<Candidate> ResolvesTicket()
    {
        if (Resolves)
        {
            return this;
        }

        var run = Run switch { null => "none", { Passed: true } => "passed", _ => "failed" };
        var verdict = Verdict is null ? "none" : Names.Of(Verdict.Decision);
        return BugFixErrors.ReportRefused.With($"{Id.Value} needs a passing run and a \"resolved\" judge decision (run: {run}, judge: {verdict})");
    }

    /// <summary>Gets a value indicating whether the candidate needs nothing more: a failing run, or a passing run with a verdict.</summary>
    public bool Evaluated => Run is not null && (!Run.Passed || Verdict is not null);

    /// <summary>Gets a value indicating whether judge was asked about this candidate and the answer was never recorded (an interrupted call).</summary>
    public bool JudgeInterrupted => Run is { Passed: true } && Verdict is null && JudgeAsked is not null;
}
