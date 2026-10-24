namespace Aw.Judge.Core.PromptSort;

/// <summary>A session's sorter state after one more prompt.</summary>
public static class SortSessionUpdate
{
    /// <summary>
    /// Counts the prompt, remembers which scaffolds fired at it and updates the UI-evidence requirement the done gate
    /// reads: a UI scaffold sets it; only a non-UI task follow-up that Jev itself judged clears it (a heuristic-only run,
    /// or a prompt that names a workflow, never does).
    /// </summary>
    /// <param name="before">The state before the prompt.</param>
    /// <param name="outcome">What the sort found.</param>
    /// <param name="plan">The scaffold plan.</param>
    /// <param name="namesWorkflow">Whether the prompt names a workflow.</param>
    /// <returns>The state after.</returns>
    public static SortSession Next(SortSession before, SortOutcome outcome, ScaffoldPlan plan, bool namesWorkflow)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentNullException.ThrowIfNull(plan);
        var prompts = before.Prompts + 1;
        var lastFired = new Dictionary<string, int>(before.LastFired, StringComparer.Ordinal);
        foreach (var id in plan.Fired)
        {
            lastFired[id] = prompts;
        }

        return new SortSession(prompts, lastFired, Requirements(before.Requirements, outcome, plan, namesWorkflow));
    }

    private static SortRequirements Requirements(SortRequirements before, SortOutcome outcome, ScaffoldPlan plan, bool namesWorkflow)
    {
        if (plan.RequiresUiEvidence)
        {
            return new SortRequirements(UiEvidence: true);
        }

        var clears = !namesWorkflow
            && string.Equals(outcome.Mode, SortOutcome.Blend, StringComparison.Ordinal)
            && outcome.Values.Flag(SortAxes.IsTask)
            && !outcome.Values.Flag(SortAxes.TouchesUi)
            && outcome.Judged(SortAxes.TouchesUi);
        return clears ? new SortRequirements() : before;
    }
}
