using System.Globalization;
using System.Text;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Features.GetJob;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// A job's model calls as text for 'sindri jobs show' (context model routing spec §8.2): the first line of a job paused on 'model-limit', and the "Model calls" block with one row per try,
/// columns time, kind, harness and model, outcome, and tokens or the reason. Codes are put into words here; the served event holds none.
/// </summary>
internal static class ModelCallText
{
    private const string ModelLimit = "model-limit";
    private const string PausedPrefix = "Paused: ";
    private const string PausedSuffix = " Resume when one is back.";
    private const int KindWidth = 15;
    private const int WhoWidth = 25;
    private const int OutcomeWidth = 11;

    /// <summary>"waiting on model-limit: claude-code at its usage limit; codex at its usage limit. Resume with: sindri jobs resume JOB-…" for a job parked on 'model-limit'; empty otherwise.</summary>
    /// <param name="job">The job.</param>
    /// <returns>The line with its newline, or empty.</returns>
    internal static string Waiting(JobView job)
    {
        if (!string.Equals(job.WaitingOn, ModelLimit, StringComparison.Ordinal))
        {
            return string.Empty;
        }

        var why = job.Progress.Count > 0 ? job.Progress[^1].Line : string.Empty;
        why = why.StartsWith(PausedPrefix, StringComparison.Ordinal) ? why[PausedPrefix.Length..] : why;
        why = (why.EndsWith(PausedSuffix, StringComparison.Ordinal) ? why[..^PausedSuffix.Length] : why).TrimEnd('.');
        return $"waiting on {ModelLimit}{(why.Length == 0 ? string.Empty : ": " + why)}. Resume with: sindri jobs resume {job.Id.Value}\n";
    }

    /// <summary>The "Model calls" block, or empty when the job made none.</summary>
    /// <param name="job">The job.</param>
    /// <param name="clock">Tells the local time zone and today.</param>
    /// <returns>The block, each line ending in a newline.</returns>
    internal static string Block(JobView job, TimeProvider clock)
    {
        if (job.ModelCalls is not { Count: > 0 } calls)
        {
            return string.Empty;
        }

        var today = clock.GetLocalNow().Date;
        var rows = calls.SelectMany(call => Tries(call, clock.LocalTimeZone)).Select(r => (Time: r.At.Date == today ? r.At.ToString("HH:mm:ss", CultureInfo.InvariantCulture) : r.At.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), r.Kind, r.Who, r.Outcome, r.Detail)).ToList();
        var timeWidth = rows.Max(static r => r.Time.Length) + 2;
        var text = new StringBuilder("Model calls\n");
        if (job.ModelCallCount > calls.Count)
        {
            _ = text.Append(string.Create(CultureInfo.InvariantCulture, $"showing the last {calls.Count} of {job.ModelCallCount}\n"));
        }

        foreach (var row in rows)
        {
            _ = text.Append((Pad(row.Time, timeWidth) + Pad(row.Kind, KindWidth) + Pad(row.Who, WhoWidth) + Pad(row.Outcome, OutcomeWidth) + row.Detail).TrimEnd()).Append('\n');
        }

        return text.ToString();
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

    private static string Trim(string number) => number.EndsWith(".0", StringComparison.Ordinal) ? number[..^2] : number;

    private static string Pad(string value, int width) => value.Length >= width ? value + " " : value.PadRight(width);

    // Each try of a call is a row; the first starts when the call does, each next one when the one before ended.
    private static IEnumerable<(DateTime At, string Kind, string Who, string Outcome, string Detail)> Tries(ContextModelCalled call, TimeZoneInfo zone)
    {
        var at = call.StartedAt;
        var tokens = call.InputTokens + (long)call.OutputTokens;
        foreach (var attempt in call.Tries)
        {
            var local = TimeZoneInfo.ConvertTime(at, zone).DateTime;
            yield return (local, call.Kind, attempt.Harness + " " + attempt.Model, Outcome(attempt.Outcome), Detail(call, attempt, tokens));
            at = at.AddMilliseconds(attempt.Ms);
        }
    }

    private static string Outcome(string outcome) => string.Equals(outcome, ModelCallOutcome.NotAsked, StringComparison.Ordinal) ? "not asked" : outcome;

    private static string Detail(ContextModelCalled call, ModelCallTry attempt, long tokens) => attempt.Outcome switch
    {
        ModelCallOutcome.Answered when call.How is { } how => Pick(call, how),
        ModelCallOutcome.Answered => tokens > 0 ? Tokens(tokens) + " tokens" : string.Empty,
        ModelCallOutcome.Failed => attempt.Reason ?? string.Empty,
        ModelCallOutcome.Skipped => string.Equals(attempt.Reason, "limited", StringComparison.Ordinal) ? "limited earlier in this job" : attempt.Reason ?? string.Empty,
        ModelCallOutcome.NotAsked => string.Equals(attempt.Reason, "no-key", StringComparison.Ordinal) ? "no key" : attempt.Reason ?? string.Empty,
        _ => string.Empty,
    };

    private static string Pick(ContextModelCalled call, ModelCallHow how) => how.Merged is not null || how.Kept is not null ? Surfaces(call, how) : Labels(call, how);

    private static string Labels(ContextModelCalled call, ModelCallHow how) =>
        Join(string.Create(CultureInfo.InvariantCulture, $"picked {how.Jev ?? 0} of {call.Items ?? 0}"), how.FirstCandidate is > 0 ? Part(how.FirstCandidate, "first") : null, how.FileName is > 0 ? Part(how.FileName, "file name") : null);

    private static string Surfaces(ContextModelCalled call, ModelCallHow how)
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
