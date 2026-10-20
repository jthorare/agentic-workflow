using System.Globalization;
using Aw.Kernel;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the Jobs page and the job sheet (API P6). Never a served sentence.</summary>
internal static partial class Labels
{
    /// <summary>The page's name (it is not a tab: G J, the palette and System's Jobs link open it).</summary>
    internal const string JobsPage = "Jobs";

    /// <summary>System's link to the page.</summary>
    internal const string JobsLink = "Background jobs";

    /// <summary>The link's accessible name: its visible words first.</summary>
    internal const string JobsLinkName = "Background jobs, opens the Jobs page";

    /// <summary>The page's one-line purpose.</summary>
    internal const string JobsPurpose = "Work Sindri does in the background, like syncing sources or building the context map.";

    /// <summary>The page before its first answer.</summary>
    internal const string ReadingJobs = "Reading jobs…";

    /// <summary>The page when an answer can't be read.</summary>
    internal const string JobsUnreadable = "The job list can't be read from this Sindri.";

    /// <summary>The page when the first read failed: the reason follows.</summary>
    internal static string JobsFailed(Error error) => "Couldn't read the jobs just now. " + InstanceRefused(error);

    /// <summary>The action beside a failed read.</summary>
    internal const string TryAgain = "Try again";

    /// <summary>The sheet before its first answer.</summary>
    internal const string ReadingJob = "Reading the job…";

    /// <summary>The sheet when Sindri doesn't know the job.</summary>
    internal const string JobUnknown = "Sindri doesn't know this job.";

    /// <summary>The sheet's progress heading.</summary>
    internal const string JobProgress = "Progress";

    /// <summary>The sheet's result heading (closed by default).</summary>
    internal const string JobResult = "What it produced";

    /// <summary>The result disclosure's note when the result was cut.</summary>
    internal const string JobResultCut = "Cut short";

    /// <summary>The line when a job has said nothing yet.</summary>
    internal const string JobNoProgress = "No progress yet.";

    /// <summary>The Details labels of the job sheet.</summary>
    internal const string JobIdField = "Job id";

    /// <summary>The kind as Sindri serves it.</summary>
    internal const string JobKindField = "Kind";

    /// <summary>The idempotency key.</summary>
    internal const string JobKeyField = "Key";

    /// <summary>The item the job is about.</summary>
    internal const string JobItemField = "Item";

    /// <summary>The job's repo.</summary>
    internal const string JobRepoField = "Repo";

    /// <summary>The service the job runs.</summary>
    internal const string JobServiceField = "Service";

    /// <summary>Who started it, as served.</summary>
    internal const string JobStartedByField = "Started by (as served)";

    /// <summary>The error code of a failed job.</summary>
    internal const string JobCodeField = "Error code";

    /// <summary>Who cancelled the job.</summary>
    internal const string JobCancelledByField = "Cancelled by";

    /// <summary>The highest progress round.</summary>
    internal const string JobRoundsField = "Rounds";

    /// <summary>The cancel's terminal form.</summary>
    internal const string JobCliField = "To cancel from a terminal";

    /// <summary>The page's head: "2 running · 5 jobs".</summary>
    /// <param name="running">Running jobs.</param>
    /// <param name="total">Jobs listed.</param>
    /// <returns>The words.</returns>
    internal static string JobsHead(int running, int total) =>
        string.Create(CultureInfo.InvariantCulture, $"{running} running · {total} {(total == 1 ? "job" : "jobs")}");

    /// <summary>A job's chip: a glyph and the state's word. A state this build doesn't know shows as itself in capitals, so a newer Sindri never fails the read.</summary>
    /// <param name="state">The state as served.</param>
    /// <returns>The glyph and the word.</returns>
    internal static (string Glyph, string Word) JobChip(string state) => state switch
    {
        "running" => ("●", "RUNNING"),
        "succeeded" => ("✓", "DONE"),
        "failed" => ("✕", "FAILED"),
        "cancelled" => ("○", "CANCELLED"),
        _ => ("○", state.ToUpperInvariant()),
    };

    /// <summary>Who started a job, in words.</summary>
    /// <param name="startedBy">The served value ("service:host", "human", "session:...").</param>
    /// <returns>The words.</returns>
    internal static string JobStartedBy(string startedBy) => startedBy switch
    {
        "service:host" => "Sindri",
        "human" => "you",
        _ when startedBy.StartsWith("service:", StringComparison.Ordinal) => "Sindri's " + startedBy["service:".Length..] + " service",
        _ when startedBy.StartsWith("session:", StringComparison.Ordinal) => "an agent session",
        _ => startedBy,
    };

    /// <summary>When a job started or ended, as a row says it.</summary>
    /// <param name="running">True while it runs.</param>
    /// <param name="age">How long since it started (running) or ended.</param>
    /// <returns>The words.</returns>
    internal static string JobWhen(bool running, TimeSpan age) => (running ? "Started " : "Ended ") + Age(age) + " ago";

    /// <summary>A job row's accessible name: the title, the state, when, who and the summary.</summary>
    /// <param name="title">The row's title.</param>
    /// <param name="word">The state's word in lower case.</param>
    /// <param name="when">When.</param>
    /// <param name="by">Who started it.</param>
    /// <param name="summary">How it ended, or empty.</param>
    /// <returns>The words.</returns>
    internal static string JobRowName(string title, string word, string when, string by, string summary) =>
        string.Join(", ", new[] { title, word, when, "by " + by, summary }.Where(p => p.Length > 0));

    /// <summary>The sheet's stale note.</summary>
    /// <param name="error">The error of the failed read.</param>
    /// <returns>The words.</returns>
    internal static string JobStale(Error error) => "Couldn't read the job just now. " + InstanceRefused(error);
}
