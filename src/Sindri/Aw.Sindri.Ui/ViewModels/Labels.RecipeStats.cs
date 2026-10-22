using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): how a workflow has run, on the Recipes page and its sheet (API P12).</summary>
internal static partial class Labels
{
    /// <summary>The window the Recipes page and the sheet start on.</summary>
    internal const string DefaultStatsWindow = "30d";

    /// <summary>The windows the API serves, shortest first.</summary>
    internal static readonly IReadOnlyList<string> StatsWindows = ["7d", "30d", "90d"];

    /// <summary>The word for a figure that is not there.</summary>
    internal const string StatNone = "–";

    /// <summary>The Runs column's header.</summary>
    internal const string ColRuns = "Runs";

    /// <summary>The Finished column's header.</summary>
    internal const string ColFinished = "Finished";

    /// <summary>The Typical time column's header.</summary>
    internal const string ColTypical = "Typical time";

    /// <summary>What the Finished column says for a workflow that only runs inside others.</summary>
    internal const string StatsChildOnly = "Runs inside others";

    /// <summary>The sheet's heading over how the workflow has run.</summary>
    internal const string StatsHeading = "How it has run";

    /// <summary>The sheet's line for a window with no runs.</summary>
    internal const string StatsNoRuns = "No runs in this window.";

    /// <summary>The sheet's line for a workflow that only runs inside others.</summary>
    internal const string StatsChildOnlyNote = "This workflow only runs inside other workflows, so it has no runs of its own.";

    /// <summary>The sheet's line when the figures can't be read.</summary>
    internal const string StatsUnavailable = "Couldn't read how this workflow has run.";

    /// <summary>The label of the sheet's runs line.</summary>
    internal const string StatRuns = "Runs";

    /// <summary>The label of the sheet's finished line.</summary>
    internal const string StatFinished = "Finished";

    /// <summary>The label of the sheet's failed line.</summary>
    internal const string StatFailed = "Failed";

    /// <summary>The label of the sheet's cancelled line.</summary>
    internal const string StatCancelled = "Cancelled";

    /// <summary>The label of the sheet's needed-a-person line.</summary>
    internal const string StatHuman = "Needed a person";

    /// <summary>The label of the sheet's typical-time line.</summary>
    internal const string StatTypical = "Typical time";

    /// <summary>The label of the sheet's typical-tokens line.</summary>
    internal const string StatTokens = "Typical tokens";

    /// <summary>The label of the sheet's budget line.</summary>
    internal const string StatBudget = "Hit the budget";

    /// <summary>A window's button words.</summary>
    /// <param name="window">'7d', '30d' or '90d'.</param>
    /// <returns>"7 days", "30 days", "90 days"; a window this build doesn't know reads as served.</returns>
    internal static string StatsWindowButton(string window) => window switch
    {
        "7d" => "7 days",
        "30d" => "30 days",
        "90d" => "90 days",
        _ => window,
    };

    /// <summary>The line that says which window the figures cover.</summary>
    /// <param name="window">The window.</param>
    /// <returns>"Last 30 days".</returns>
    internal static string StatsWindowLine(string window) => "Last " + StatsWindowButton(window);

    /// <summary>A share as a whole percent.</summary>
    /// <param name="done">The part.</param>
    /// <param name="of">The whole; zero reads as no figure.</param>
    /// <returns>"80%", or the none word.</returns>
    internal static string StatShare(int done, int of) => of <= 0 ? StatNone : Percent(done / (double)of);

    /// <summary>A count with thousands separators.</summary>
    /// <param name="n">The count.</param>
    /// <returns>"12,400".</returns>
    internal static string StatCount(long n) => n.ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>The heading over where a person came into the runs.</summary>
    internal const string StatsHumanHeading = "Where a person came in";

    /// <summary>The heading over the per-step figures.</summary>
    internal const string StatsStepsHeading = "By step";

    /// <summary>The heading over the weekly figures.</summary>
    internal const string StatsWeeksHeading = "By week";

    /// <summary>The label of the answered-parks line.</summary>
    internal const string StatParksAnswered = "Parks answered";

    /// <summary>The label of the decided-escalations line.</summary>
    internal const string StatEscalationsDecided = "Escalations decided";

    /// <summary>The label of the takeovers line.</summary>
    internal const string StatTakeovers = "Takeovers";

    /// <summary>A count and the typical wait for it.</summary>
    /// <param name="n">How many times.</param>
    /// <param name="seconds">The typical wait, or null.</param>
    /// <returns>"3 · 30m", or just "3" when there is no wait.</returns>
    internal static string StatCountAndWait(int n, long? seconds) => seconds is { } s ? $"{StatCount(n)} \u00b7 {Duration(s)}" : StatCount(n);

    /// <summary>One step's figures in words.</summary>
    /// <param name="parts">The parts that apply (the typical time, the parks, the escalations, the takeovers, the gate), already in words.</param>
    /// <returns>The parts joined, or the none word when there are none.</returns>
    internal static string StatStepLine(IReadOnlyList<string> parts) => parts.Count == 0 ? StatNone : string.Join(" \u00b7 ", parts);

    /// <summary>A park or escalation reason in words.</summary>
    /// <param name="reason">The reason as served.</param>
    /// <returns>"no progress" for "no-progress".</returns>
    internal static string StatReason(string reason) => reason.Replace('-', ' ');

    /// <summary>A week's label.</summary>
    /// <param name="weekStart">The Monday as served, yyyy-MM-dd.</param>
    /// <returns>"Week of 29 Dec"; a date this build can't read is shown as served.</returns>
    internal static string StatWeek(string weekStart) =>
        "Week of " + (DateOnly.TryParseExact(weekStart, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) ? day.ToString("d MMM", CultureInfo.InvariantCulture) : weekStart);

    /// <summary>A week's figures in words.</summary>
    /// <param name="runs">Runs that started that week.</param>
    /// <param name="humanRate">The share that needed a person, or null.</param>
    /// <returns>"4 runs · 25% needed a person".</returns>
    internal static string StatWeekLine(int runs, double? humanRate) =>
        $"{StatCount(runs)} {(runs == 1 ? "run" : "runs")}" + (humanRate is { } rate ? $" \u00b7 {Percent(rate)} needed a person" : string.Empty);
}
