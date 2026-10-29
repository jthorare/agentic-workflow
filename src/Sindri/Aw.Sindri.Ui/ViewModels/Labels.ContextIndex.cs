using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): Health's Context index card (#1597). The words are keyed by each source's pull state; none is a served sentence.</summary>
internal static partial class Labels
{
    /// <summary>The card's heading.</summary>
    internal const string ContextIndexCard = "Context index";

    /// <summary>A line's accessible name starts with this, then the line.</summary>
    internal const string ContextIndexNamePrefix = "Context index: ";

    /// <summary>The card before its first answer.</summary>
    internal const string ReadingContextIndex = "Reading the context index…";

    /// <summary>The card when no source is set up.</summary>
    internal const string ContextIndexNone = "No sources are set up yet.";

    /// <summary>The card when the read failed.</summary>
    internal const string ContextIndexUnreadable = "The context index can't be read just now.";

    /// <summary>The Context index, named in the headline when its card needs a look.</summary>
    internal const string ContextIndexLookName = "context index";

    /// <summary>A source that hasn't started and has no phase to show.</summary>
    internal const string ContextSourceNotStarted = "not started";

    /// <summary>A source that is pulling.</summary>
    internal const string ContextSourcePulling = "pulling";

    /// <summary>A source between pulls.</summary>
    internal const string ContextSourceIdle = "idle";

    /// <summary>A source that stopped early because it hit its cap this round (SND-CONTEXT-059).</summary>
    internal const string ContextSourceAtCap = "idle, at its cap this round";

    /// <summary>The code a source reports when it was capped this tick.</summary>
    internal const string ContextCapCode = "SND-CONTEXT-059";

    /// <summary>What a source in backoff reads as.</summary>
    /// <param name="minutes">Whole minutes until the next try, at least 1.</param>
    /// <param name="code">The last error's code, or null.</param>
    /// <returns>"retrying in 4 min (SND-CONTEXT-041)".</returns>
    internal static string ContextSourceRetrying(int minutes, string? code)
    {
        var words = string.Create(CultureInfo.InvariantCulture, $"retrying in {minutes} min");
        return string.IsNullOrEmpty(code) ? words : words + " (" + code + ")";
    }

    /// <summary>What a failed source reads as.</summary>
    /// <param name="code">The last error's code, or null.</param>
    /// <param name="text">The one scrubbed line saying what failed, or null.</param>
    /// <returns>"failed (SND-CONTEXT-041): the token was refused".</returns>
    internal static string ContextSourceFailed(string? code, string? text)
    {
        var words = string.IsNullOrEmpty(code) ? "failed" : "failed (" + code + ")";
        return string.IsNullOrEmpty(text) ? words : words + ": " + text;
    }

    /// <summary>How many records a source holds.</summary>
    /// <param name="records">The count.</param>
    /// <returns>"200 records".</returns>
    internal static string ContextRecords(int records) =>
        string.Create(CultureInfo.InvariantCulture, $"{records} {(records == 1 ? "record" : "records")}");

    /// <summary>The words when a source has records but its last commit is unknown.</summary>
    internal const string ContextNothingCommitted = "nothing committed yet";

    /// <summary>When a source last committed.</summary>
    /// <param name="age">Since the last successful tick.</param>
    /// <returns>"last commit 3 min ago".</returns>
    internal static string ContextLastCommit(TimeSpan age) => "last commit " + Ago(age);

    /// <summary>When concept synthesis last ran, and its code.</summary>
    /// <param name="age">Since the last pass, or null when unknown.</param>
    /// <param name="code">The last pass's code, or null.</param>
    /// <returns>"last run 3 min ago (SND-CONTEXT-030)", "last run 2 h ago" or "last run (SND-CONTEXT-030)".</returns>
    internal static string ConceptsLastRun(TimeSpan? age, string? code)
    {
        var words = age is { } a ? "last run " + Ago(a) : "last run";
        return string.IsNullOrEmpty(code) ? words : words + " (" + code + ")";
    }

    private static string Ago(TimeSpan age) => age.TotalMinutes switch
    {
        < 1 => "under a minute ago",
        < 60 => string.Create(CultureInfo.InvariantCulture, $"{(int)age.TotalMinutes} min ago"),
        < 1440 => string.Create(CultureInfo.InvariantCulture, $"{(int)age.TotalHours} h ago"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{(int)age.TotalDays} d ago"),
    };

    /// <summary>The concepts line when concept synthesis or labeling is off.</summary>
    internal const string ConceptsOff = "Concepts: labeling is off";

    /// <summary>The concepts line when their read failed.</summary>
    /// <param name="code">The failure's code.</param>
    /// <returns>"Concepts: can't be read (SND-CONTEXT-090)".</returns>
    internal static string ConceptsUnreadable(string code) => "Concepts: can't be read (" + code + ")";

    /// <summary>How many concepts there are and how many cite two or more sources.</summary>
    /// <param name="concepts">Stored concepts.</param>
    /// <param name="multiSource">Those citing two or more sources.</param>
    /// <returns>"Concepts: 101 · 34 cite 2+ sources (33%)", or "Concepts: none yet".</returns>
    internal static string ConceptsCount(int concepts, int multiSource) => concepts <= 0
        ? "Concepts: none yet"
        : string.Create(CultureInfo.InvariantCulture, $"Concepts: {concepts} · {multiSource} cite 2+ sources ({multiSource * 100 / concepts}%)");

    /// <summary>Records with no vector yet.</summary>
    /// <param name="pending">The count.</param>
    /// <returns>"Not embedded yet: 12 records".</returns>
    internal static string ConceptsNotEmbedded(int pending) => "Not embedded yet: " + ContextRecords(pending);

    /// <summary>Groups waiting for the model to name them.</summary>
    /// <param name="queued">The count.</param>
    /// <returns>"Waiting for a name: 3 groups".</returns>
    internal static string ConceptsQueued(int queued) =>
        string.Create(CultureInfo.InvariantCulture, $"Waiting for a name: {queued} {(queued == 1 ? "group" : "groups")}");

    /// <summary>Embedded records in no concept: most are singletons the grouper never names, so this is normal, not a backlog.</summary>
    /// <param name="records">Embedded minus covered.</param>
    /// <param name="coverage">Covered over embedded, 0 to 1.</param>
    /// <returns>"Not in any concept: 250 records (55% covered)"; no share while nothing is covered.</returns>
    internal static string ConceptsUncovered(int records, double coverage)
    {
        var words = "Not in any concept: " + ContextRecords(records);
        var percent = (int)Math.Round(Math.Clamp(coverage, 0, 1) * 100, MidpointRounding.AwayFromZero);   // rounded like `sindri index status`
        return percent <= 0 ? words : words + string.Create(CultureInfo.InvariantCulture, $" ({percent}% covered)");
    }
}
