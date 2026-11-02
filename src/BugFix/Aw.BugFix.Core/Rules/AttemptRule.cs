using System.Globalization;
using Aw.BugFix.Core.Errors;
using Aw.BugFix.Core.Model;
using Aw.Kernel;

namespace Aw.BugFix.Core.Rules;

/// <summary>When a new attempt may start, and how many candidates an attempt runs: one decision for start-attempt and resume.</summary>
public static class AttemptRule
{
    /// <summary>How many candidates an attempt in <paramref name="mode"/> runs: two competing ones in mode B, otherwise one.</summary>
    /// <param name="mode">The attempt's mode.</param>
    /// <returns>1 or 2.</returns>
    public static int Candidates(FixMode? mode) => mode == FixMode.B ? 2 : 1;

    /// <summary>Whether the next attempt may start in <paramref name="mode"/>.</summary>
    /// <param name="state">The bug fix.</param>
    /// <param name="mode">The requested mode.</param>
    /// <param name="abandonJudge">Whether to leave a candidate whose judge question was never answered (an interrupted call) behind.</param>
    /// <param name="abandonUnrun">Whether to leave a candidate with no recorded run behind (record-run refused it, so it can't be evaluated).</param>
    /// <returns>Success, or AW-BUGFIX-010 saying why not.</returns>
    public static Result<Unit> CanStart(BugFixState state, FixMode mode, bool abandonJudge = false, bool abandonUnrun = false)
    {
        if (Pending(state, abandonJudge, abandonUnrun) is { } pending)
        {
            return pending;
        }

        if (state.Attempt >= BugFixState.MaxAttempts)
        {
            return BugFixErrors.AttemptRefused.With(string.Create(CultureInfo.InvariantCulture, $"attempt cap ({BugFixState.MaxAttempts}) reached; run advance report --unresolved"));
        }

        if (mode != FixMode.B && state.Attempt == 0 && !state.Confirmed)
        {
            return BugFixErrors.AttemptRefused.With("no hypothesis is confirmed; start with mode B (competing implementers, one per open hypothesis)");
        }

        return mode == FixMode.B ? Competing(state) : Results.Ok();
    }

    // A candidate that resolves the ticket, or one not evaluated yet, holds the next attempt back.
    private static Error? Pending(BugFixState state, bool abandonJudge, bool abandonUnrun)
    {
        var current = state.CurrentCandidates.ToList();
        if (current.Find(static c => c.Resolves) is { } resolving)
        {
            return BugFixErrors.AttemptRefused.With($"candidate {resolving.Id.Value} already resolves the ticket; run advance report --candidate {resolving.Id.Value}");
        }

        if (current.Find(c => !c.Evaluated && !(abandonJudge && c.JudgeInterrupted) && !(abandonUnrun && c.Run is null)) is { } pending)
        {
            return BugFixErrors.AttemptRefused.With(pending.JudgeInterrupted
                ? $"judge was asked about {pending.Id.Value} and its answer was never recorded; if no judge process is still running, leave it with start-attempt --abandon-judge"
                : $"candidate {pending.Id.Value} has not been evaluated yet");
        }

        return null;
    }

    // Mode B (competing implementers) is the escalation after a failed attempt, one implementer per open hypothesis.
    private static Result<Unit> Competing(BugFixState state)
    {
        if (state.Attempt == 0 && state.Confirmed)
        {
            return BugFixErrors.AttemptRefused.With("mode B (competing implementers) is the escalation after a failed attempt, or the first attempt when no hypothesis is confirmed");
        }

        var open = state.Investigation?.Hypotheses.Count(static h => h.Result != HypothesisResult.RuledOut) ?? 0;
        return open >= 2
            ? Results.Ok()
            : BugFixErrors.AttemptRefused.With(string.Create(CultureInfo.InvariantCulture, $"mode B needs at least 2 hypotheses not ruled out; the handoff has {open}"));
    }
}
