using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Run;

/// <summary>The inspector's step timeline from the instance's served Step runs (F3 spec 3 §4): the tries of one step, in the order they started, each in plain words.</summary>
internal static class RunTimeline
{
    private const string Running = "running";

    /// <summary>The rows of one step.</summary>
    /// <param name="runs">The instance's runs, as served.</param>
    /// <param name="step">The step's state id.</param>
    /// <param name="now">The time elapsed is counted to.</param>
    /// <returns>One row per try, oldest first.</returns>
    internal static IReadOnlyList<RunTimelineRow> Of(IReadOnlyList<StepRun>? runs, string step, DateTimeOffset now) =>
        [.. (runs ?? []).Where(r => string.Equals(r.Step, step, StringComparison.Ordinal)).OrderBy(r => r.Attempt).Select(r => Row(r, now))];

    private static RunTimelineRow Row(StepRun run, DateTimeOffset now)
    {
        var state = StateOf(run.State);
        var brushes = RunStates.Brushes(state);
        var word = RunStates.Word(state);
        var tryText = Labels.StepTry(run.Attempt, run.MaxAttempts);
        var times = Times(run, now);
        var note = string.Equals(run.State, "refused", StringComparison.Ordinal) ? Labels.RunRefusedByCheck : string.Empty;
        var raw = string.Join(' ', run.Token, run.State, run.Outcome ?? "-", run.Actor ?? run.Runtime ?? "-");
        return new RunTimelineRow(run.Token, tryText, state, word, RunStateIcons.Of(state), brushes.Text, note, times, Who(run),
            run.Tokens is { } tokens ? Labels.RunTokensUsed(tokens) : string.Empty, raw, Labels.RunTimelineName(tryText, word, times));
    }

    /// <summary>The state a served run state reads as.</summary>
    /// <param name="served">running, passed, failed, refused, cancelled or abandoned.</param>
    /// <returns>The state; a refused run reads failed.</returns>
    internal static RunNodeState StateOf(string served) => served switch
    {
        Running => RunNodeState.Running,
        "passed" => RunNodeState.Done,
        "failed" or "refused" => RunNodeState.Failed,
        "cancelled" or "abandoned" => RunNodeState.Cancelled,
        _ => RunNodeState.NotStarted,
    };

    private static string Times(StepRun run, DateTimeOffset now)
    {
        if (string.Equals(run.State, Running, StringComparison.Ordinal))
        {
            return Labels.RunRunningFor(Labels.Duration((long)Math.Max(0, (now - run.StartedAt).TotalSeconds)));
        }

        return run.DurationMs is { } ms ? Labels.RunTook(Labels.RunDuration(ms)) : Labels.RunNoTimes;
    }

    // A closed run names who closed it; an open one, how it runs. Nothing else is guessed.
    private static string Who(StepRun run)
    {
        var actor = run.Actor ?? string.Empty;
        if (actor.StartsWith("human", StringComparison.Ordinal))
        {
            return Labels.KindYou;
        }

        if (actor.StartsWith("agent", StringComparison.Ordinal) || actor.StartsWith("session", StringComparison.Ordinal))
        {
            return Labels.KindAgent;
        }

        return !string.Equals(run.State, Running, StringComparison.Ordinal) ? string.Empty
            : string.Equals(run.Runtime, "agent", StringComparison.Ordinal) ? Labels.KindAgent
            : string.Equals(run.Runtime, "desk", StringComparison.Ordinal) ? Labels.KindYou
            : string.Empty;
    }
}
