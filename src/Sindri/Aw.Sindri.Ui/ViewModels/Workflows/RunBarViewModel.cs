namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>One slot of a list row's strip of the last 10 runs: its outcome, its shape (a failed run is tall, a cancelled one hollow, so colour is never the only cue) and the two tooltip lines.</summary>
internal sealed class RunBarViewModel
{
    private RunBarViewModel(string kind, string titleLine, string detailLine) => (Kind, TitleLine, DetailLine) = (kind, titleLine, detailLine);

    /// <summary>An empty slot: a short dash.</summary>
    internal static RunBarViewModel Empty { get; } = new("empty", string.Empty, string.Empty);

    /// <summary>One run's bar.</summary>
    /// <param name="run">The served run.</param>
    /// <param name="clock">The clock the age and the date run on.</param>
    /// <returns>The bar.</returns>
    internal static RunBarViewModel Of(Api.Workflows.WorkflowRecentRunView run, TimeProvider clock)
    {
        var status = run.Status.ToLowerInvariant();
        var kind = status is "queued" or "waiting" ? "running" : status;
        var number = run.Run[(run.Run.LastIndexOf(':') + 1)..];
        var local = TimeZoneInfo.ConvertTime(run.StartedAt, clock.LocalTimeZone);
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        var detail = local.ToString("ddd d MMM yyyy", culture) + " \u00b7 " + local.ToString("HH:mm:ss", culture);
        if (run.Seconds is { } seconds)
        {
            detail += string.Create(culture, $" \u00b7 {Math.Round(seconds)} s");
        }

        return new RunBarViewModel(kind, $"Run {number} \u00b7 {status} \u00b7 {Labels.AgoWords(clock.GetUtcNow() - run.StartedAt)}", detail);
    }

    /// <summary>The outcome: succeeded, failed, cancelled, running or empty.</summary>
    public string Kind { get; }

    /// <summary>True for a failed run: a tall bar.</summary>
    public bool IsTall => string.Equals(Kind, "failed", StringComparison.Ordinal);

    /// <summary>True for a cancelled run: a hollow bar.</summary>
    public bool IsHollow => string.Equals(Kind, "cancelled", StringComparison.Ordinal);

    /// <summary>True for an empty slot.</summary>
    public bool IsEmpty => string.Equals(Kind, "empty", StringComparison.Ordinal);

    /// <summary>True for a bar drawn in the success colour (succeeded, or still running).</summary>
    public bool IsOk => Kind is "succeeded" or "running";

    /// <summary>The bar's height: tall for a failed run, short for the others, a dash for an empty slot.</summary>
    public double BarHeight => IsTall ? 16 : IsEmpty ? 2 : 8;

    /// <summary>The tooltip: the two lines joined, or null for an empty slot (no tooltip).</summary>
    public string? TipText => IsEmpty ? null : TitleLine + "\n" + DetailLine;

    /// <summary>"Run 499 \u00b7 failed \u00b7 22m ago"; empty for an empty slot.</summary>
    public string TitleLine { get; }

    /// <summary>"Sat 14 Nov 2026 \u00b7 10:20:18 \u00b7 10 s"; empty for an empty slot.</summary>
    public string DetailLine { get; }
}
