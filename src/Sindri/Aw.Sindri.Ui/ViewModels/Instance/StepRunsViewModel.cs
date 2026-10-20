using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>
/// The instance frame's Step runs in plain words: the current stage's run line ("Attempt 2 of 3, with you") and a closed
/// Runs disclosure with one row per run. Every word is a served field mapped to the app's copy; an older server that
/// serves no runs draws nothing.
/// </summary>
internal sealed class StepRunsViewModel
{
    private StepRunsViewModel(string currentLine, IReadOnlyList<StepRunRowViewModel> rows)
    {
        (CurrentLine, Rows) = (currentLine, rows);
    }

    /// <summary>The current stage's run line, or empty when no run is open.</summary>
    public string CurrentLine { get; }

    /// <summary>True when a run is open on the current stage.</summary>
    public bool HasCurrentLine => CurrentLine.Length > 0;

    /// <summary>One row per run, in served (start) order.</summary>
    public IReadOnlyList<StepRunRowViewModel> Rows { get; }

    /// <summary>True when there is any run to list.</summary>
    public bool HasRows => Rows.Count > 0;

    /// <summary>The disclosure's heading.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string Heading => Labels.RunsHeading;
#pragma warning restore CA1822, MA0041

    /// <summary>What the list draws, so a re-read of the same keeps it.</summary>
    internal string Signature => string.Join('|', CurrentLine, string.Join(';', Rows.Select(r => r.Text)));

    /// <summary>Reads the served runs.</summary>
    /// <param name="runs">The runs as served; null from a server that predates them.</param>
    /// <param name="currentState">The instance's current state.</param>
    /// <param name="engineOwned">True when the engine owns the instance (parked, escalated or taken over); nobody is "with" the run then.</param>
    /// <returns>The view model; empty without runs.</returns>
    internal static StepRunsViewModel Of(IReadOnlyList<StepRun>? runs, string currentState, bool engineOwned = false)
    {
        if (runs is not { Count: > 0 })
        {
            return new StepRunsViewModel(string.Empty, []);
        }

        var open = runs.LastOrDefault(r => IsOpen(r) && string.Equals(r.Step, currentState, StringComparison.Ordinal));
        var line = open is null ? string.Empty : Join(", ", Labels.AttemptWords(open.Attempt, open.MaxAttempts), engineOwned ? string.Empty : Who(open));
        return new StepRunsViewModel(line, [.. runs.Select(Row)]);
    }

    private static bool IsOpen(StepRun run) => string.Equals(run.State, "running", StringComparison.Ordinal);

    private static StepRunRowViewModel Row(StepRun run)
    {
        var step = Labels.ReadableWord(run.Step);
        var attempt = Labels.AttemptWords(run.Attempt, run.MaxAttempts);
        var state = Labels.RunStateWord(run.State);
        var took = run.DurationMs is { } ms ? Labels.RunDuration(ms) : string.Empty;
        var text = Join(" · ", step, attempt, state, took, Who(run));
        var name = Join(", ", step, attempt.ToLowerInvariant(), state.ToLowerInvariant(), took.Length > 0 ? "took " + took : string.Empty, Who(run));
        return new StepRunRowViewModel(text, name);
    }

    // The closing actor in words. Only an open run falls back to how it runs; a closed run whose actor is not a person or an agent says nothing.
    private static string Who(StepRun run)
    {
        var open = IsOpen(run);
        var actor = run.Actor ?? string.Empty;
        var you = actor.StartsWith("human", StringComparison.Ordinal);
        var agent = actor.StartsWith("agent", StringComparison.Ordinal) || actor.StartsWith("session", StringComparison.Ordinal);
        if (!you && !agent && open)
        {
            you = string.Equals(run.Runtime, "desk", StringComparison.Ordinal);
            agent = string.Equals(run.Runtime, "agent", StringComparison.Ordinal);
        }

        return you ? (open ? Labels.RunWithYou : Labels.RunByYou) : agent ? (open ? Labels.RunWithAgent : Labels.RunByAgent) : string.Empty;
    }

    private static string Join(string separator, params string[] parts) => string.Join(separator, parts.Where(p => p.Length > 0));
}
