using Aw.BugFix.Core.Model;

namespace Aw.BugFix.Core.Rules;

/// <summary>What the orchestrator should do next, from the state alone (resume's answer).</summary>
public static class NextStep
{
    /// <summary>The next action.</summary>
    /// <param name="state">The bug fix.</param>
    /// <returns>One instruction line.</returns>
    public static string For(BugFixState state)
    {
        if (state.Finished)
        {
            return state.Status == BugFixStatus.Resolved
                ? "done (resolved); write resolution.md if not written"
                : "done (unresolved); write resolution.md if not written";
        }

        // A resolving candidate wins over an escalation of another one in the same attempt.
        if (state.CurrentCandidates.FirstOrDefault(static c => c.Resolves) is { } resolving && state.Phase == Phase.Evaluate)
        {
            return $"advance report --candidate {resolving.Id.Value}";
        }

        return state.Phase switch
        {
            Phase.Intake => "run /rootCause --investigate-only, then advance investigate --evidence <handoff.md>",
            Phase.Investigate => "build the check (ui-evidence script or regression test), run it on the unfixed code, then advance reproduce",
            Phase.Reproduce => state.Confirmed ? "start-attempt --mode A or C" : "start-attempt --mode B (no hypothesis is confirmed: competing implementers, one per open hypothesis)",
            Phase.Fix or Phase.Evaluate => InAttempt(state),
            Phase.Report => "done",
            _ => throw new ArgumentOutOfRangeException(nameof(state), state.Phase, "not a phase"),
        };
    }

    // Phase 5 runs in the main checkout detached at the candidate, possibly left that way by an interrupted session,
    // so the step always names the exact commit.
    private static string InAttempt(BugFixState state)
    {
        var current = state.CurrentCandidates.ToList();
        if (current.Find(static c => c.Resolves) is { } resolving)
        {
            return $"advance report --candidate {resolving.Id.Value}";
        }

        if (current.Find(static c => c.Run is null) is { } unrun)
        {
            return $"detach the main checkout at {unrun.Commit} ({unrun.Id.Value}), run the same check, then record-run {unrun.Id.Value}";
        }

        if (Interrupted(current) is { } interrupted)
        {
            return interrupted;
        }

        if (current.Find(static c => !c.Evaluated) is { } unjudged)
        {
            var again = unjudged.JudgeEscalations > 0 ? " again (judge escalated once; one more ask, then it falls back to unresolved)" : string.Empty;
            return $"judge {unjudged.Id.Value}{again} (if judge is unavailable, exit 3: retry with backoff, up to 3 tries, then advance report --unresolved)";
        }

        if (current.Count == 0)
        {
            return "dispatch the implementer(s), then record-candidate";
        }

        if (current.Count < AttemptRule.Candidates(state.Mode))
        {
            return "record-candidate for the second open hypothesis (mode B runs two competing candidates); if its implementer produced no usable commit, "
                + Onward(state);
        }

        return Onward(state);
    }

    private static string Onward(BugFixState state) =>
        AttemptRule.CanStart(state, FixMode.A).IsSuccess ? "start-attempt with the failure reasons" : "advance report --unresolved";

    // A question judge was asked whose answer was never recorded: asked once more, then left behind once no judge process is running.
    private static string? Interrupted(List<Candidate> current) =>
        current.Find(static c => c.JudgeInterrupted) is { } asked && current.TrueForAll(static c => c.Evaluated || c.JudgeInterrupted)
            ? asked.JudgeAsks < Candidate.MaxJudgeAsks
                ? $"judge {asked.Id.Value} again (its first call was interrupted and left no answer; one retry is allowed)"
                : $"judge was asked about {asked.Id.Value} {Candidate.MaxJudgeAsks} times without a recorded answer; if no judge process is still running (see judge why), start-attempt --mode <A|B|C> --abandon-judge, otherwise wait for it, or advance report --unresolved"
            : null;
}
