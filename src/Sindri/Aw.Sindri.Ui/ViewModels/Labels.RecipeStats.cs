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
}
