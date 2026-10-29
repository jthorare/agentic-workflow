using System.Globalization;
using Aw.Sindri.Ui.Api.Jobs;

namespace Aw.Sindri.Ui.ViewModels.Jobs;

/// <summary>The words of a job's model calls (context model routing spec §8.2): the Jobs row's line, and the sheet's rows. Pure; the views only bind what it returns.</summary>
internal static class ModelLines
{
    /// <summary>The signal a job parked for a model harness waits on.</summary>
    internal const string ModelLimit = "model-limit";

    private const int MostPairs = 2;

    /// <summary>The Jobs row's model line, or null when the job made no call and isn't paused on a limit.</summary>
    /// <param name="row">The job as served.</param>
    /// <returns>The line.</returns>
    internal static string? Row(JobRowView row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (string.Equals(row.WaitingOn, ModelLimit, StringComparison.Ordinal))
        {
            return row.WaitingLine is { Length: > 0 } line ? line : Labels.JobModelPausedFallback;
        }

        return row.Models is { Calls: > 0 } totals ? totals.AnsweredBy.Count == 0 ? Failed(totals) : Line(totals) : null;
    }

    /// <summary>A token count short: 750, 4.1k, 150k, 1.2M.</summary>
    /// <param name="tokens">The count.</param>
    /// <returns>The words.</returns>
    internal static string Tokens(long tokens) => tokens switch
    {
        < 1_000 => tokens.ToString(CultureInfo.InvariantCulture),
        < 100_000 => Trim((tokens / 1_000.0).ToString("0.0", CultureInfo.InvariantCulture)) + "k",
        < 1_000_000 => (tokens / 1_000).ToString(CultureInfo.InvariantCulture) + "k",
        _ => Trim((tokens / 1_000_000.0).ToString("0.0", CultureInfo.InvariantCulture)) + "M",
    };

    /// <summary>The sheet's rows, one per try, in call order.</summary>
    /// <param name="job">The job as served.</param>
    /// <param name="clock">Tells the local time zone and today.</param>
    /// <returns>The rows.</returns>
    internal static IReadOnlyList<JobModelRowViewModel> Calls(JobView job, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(clock);
        var today = clock.GetLocalNow().Date;
        return [.. (job.ModelCalls ?? []).SelectMany(call => Tries(call, clock.LocalTimeZone, today))];
    }

    /// <summary>"showing the last 200 of 205" when the job made more calls than the sheet holds; else null.</summary>
    /// <param name="job">The job as served.</param>
    /// <returns>The words, or null.</returns>
    internal static string? More(JobView job)
    {
        ArgumentNullException.ThrowIfNull(job);
        var shown = job.ModelCalls?.Count ?? 0;
        return job.ModelCallCount > shown ? string.Create(CultureInfo.InvariantCulture, $"showing the last {shown} of {job.ModelCallCount}") : null;
    }

    // "(3 failed)" when calls failed; a job whose calls were all skipped or not asked has none failed, so it counts the skipped ones.
    private static string Failed(JobModelTotalsView totals) => totals.FailedCalls > 0
        ? string.Create(CultureInfo.InvariantCulture, $"Models: no call answered ({totals.FailedCalls} failed)")
        : string.Create(CultureInfo.InvariantCulture, $"Models: no call answered ({totals.Calls - totals.FailedCalls} skipped)");

    private static string Line(JobModelTotalsView totals)
    {
        var named = totals.AnsweredBy.Take(MostPairs).Select(static a => string.Create(CultureInfo.InvariantCulture, $"{a.Harness} {a.Model} {a.Count} {(a.Count == 1 ? "call" : "calls")}"));
        var more = totals.AnsweredBy.Count - MostPairs;
        var tokens = totals.InputTokens + totals.OutputTokens;
        var line = "Models: " + string.Join(", ", named) + (more > 0 ? string.Create(CultureInfo.InvariantCulture, $" +{more} more") : string.Empty)
            + (tokens > 0 ? " · " + Tokens(tokens) + " tokens (input plus output)" : string.Empty);
        return totals.FellBack > 0 ? line + string.Create(CultureInfo.InvariantCulture, $" ({totals.FellBack} fell back)") : line;
    }

    private static string Trim(string number) => number.EndsWith(".0", StringComparison.Ordinal) ? number[..^2] : number;

    // The first try starts when its call does, each next one when the one before ended.
    private static IEnumerable<JobModelRowViewModel> Tries(JobModelCallView call, TimeZoneInfo zone, DateTime today)
    {
        var at = call.StartedAt;
        var tokens = call.InputTokens + (long)call.OutputTokens;
        foreach (var attempt in call.Tries)
        {
            var local = TimeZoneInfo.ConvertTime(at, zone).DateTime;
            var time = local.Date == today ? local.ToString("HH:mm:ss", CultureInfo.InvariantCulture) : local.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            yield return string.Equals(attempt.Harness, "jev", StringComparison.Ordinal) ? Jev(time, call, attempt) : Row(time, call, attempt, tokens);
            at = at.AddMilliseconds(attempt.Ms);
        }
    }

    private static JobModelRowViewModel Row(string time, JobModelCallView call, JobModelTryView attempt, long tokens) =>
        new(time, call.Kind, attempt.Harness + " " + attempt.Model, Detail(attempt, tokens), string.Equals(attempt.Outcome, "not-asked", StringComparison.Ordinal) ? "not asked" : attempt.Outcome);

    private static string Detail(JobModelTryView attempt, long tokens) => attempt.Outcome switch
    {
        "answered" => tokens > 0 ? Tokens(tokens) + " tokens" : string.Empty,
        "failed" => attempt.Reason ?? string.Empty,
        "skipped" => string.Equals(attempt.Reason, "limited", StringComparison.Ordinal) ? "limited earlier in this job" : attempt.Reason ?? string.Empty,
        _ => string.Empty,
    };

    // A Jev pick is one sentence: what it settled, or why it was not asked.
    private static JobModelRowViewModel Jev(string time, JobModelCallView call, JobModelTryView attempt)
    {
        var surfaces = call.How is { Merged: not null } or { Kept: not null } || string.Equals(call.Kind, "productSurfaces", StringComparison.Ordinal);
        var head = surfaces ? "Jev product surfaces: " : "Jev: ";
        var said = string.Equals(attempt.Outcome, "not-asked", StringComparison.Ordinal) ? "not asked (" + NotAsked(attempt.Reason) + ")"
            : call.How is { } how ? surfaces ? SurfacePick(call, how) : LabelPick(call, how) : attempt.Outcome;
        return new JobModelRowViewModel(time, string.Empty, head + said, string.Empty, string.Empty);
    }

    private static string NotAsked(string? reason) => string.Equals(reason, "no-key", StringComparison.Ordinal) ? "no key" : reason ?? "off";

    private static string LabelPick(JobModelCallView call, JobModelHowView how) =>
        Join(string.Create(CultureInfo.InvariantCulture, $"picked {how.Jev ?? 0} of {call.Items ?? 0}"), how.FirstCandidate is > 0 ? Part(how.FirstCandidate, "took the model's first") : null, how.FileName is > 0 ? Part(how.FileName, "took the file name") : null);

    private static string SurfacePick(JobModelCallView call, JobModelHowView how)
    {
        var kept = Part(how.Kept, "kept");
        if (kept is not null && how.Undecided is > 0)
        {
            kept += " (" + Part(how.Undecided, "undecided") + ")";
        }

        return Join(Part(call.Items, "groups"), Part(how.Merged, "merged"), Part(how.Rejected, "rejected"), kept);
    }

    private static string? Part(int? count, string word) => count is { } n ? string.Create(CultureInfo.InvariantCulture, $"{n} {word}") : null;

    private static string Join(params string?[] parts) => string.Join(", ", parts.OfType<string>());
}
