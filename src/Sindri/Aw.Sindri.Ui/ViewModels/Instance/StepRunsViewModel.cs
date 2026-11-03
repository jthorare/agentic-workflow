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
        var line = open is null ? string.Empty : Join(", ", Labels.AttemptWords(open.Attempt, open.MaxAttempts), engineOwned ? string.Empty : Waits(open));
        return new StepRunsViewModel(line, [.. runs.Select(Row)]);
    }

    // The current line says what an open desk run waits for; a run with an agent keeps who is on it.
    private static string Waits(StepRun run)
    {
        var who = Who(run);
        return string.Equals(who, Labels.RunWithYou, StringComparison.Ordinal) ? Labels.RunWaitingForYou : who;
    }

    private static bool IsOpen(StepRun run) => string.Equals(run.State, "running", StringComparison.Ordinal);

    private static StepRunRowViewModel Row(StepRun run)
    {
        var step = Labels.ReadableWord(run.Step);
        var attempt = Labels.AttemptWords(run.Attempt, run.MaxAttempts);
        var state = Labels.RunStateWord(run.State);
        var took = run.DurationMs is { } ms ? Labels.RunDuration(ms) : string.Empty;
        var images = ImageWords(run);
        var text = Join(" · ", step, attempt, state, took, Who(run), images);
        var name = Join(", ", step, attempt.ToLowerInvariant(), state.ToLowerInvariant(), took.Length > 0 ? "took " + took : string.Empty, Who(run), images);
        return new StepRunRowViewModel(text, name);
    }

    // A judge choice that looked at images says so (images spec 5.1): "decided with 2 images", "decided with 4 images (2 left out)"; nothing at 0.
    private static string ImageWords(StepRun run)
    {
        if (run.ImageCount is not { } count || count <= 0)
        {
            return string.Empty;
        }

        var words = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"decided with {count} {(count == 1 ? "image" : "images")}");
        return run.ImagesDropped is > 0 and var dropped
            ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{words} ({dropped} left out)")
            : words;
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
