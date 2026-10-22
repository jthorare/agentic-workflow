using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the Workflows board, list and run drawer (F3 workflows spec §6). Never a served sentence.</summary>
internal static partial class Labels
{
    /// <summary>A run that is working.</summary>
    internal const string WorkflowsStatusRunning = "Running";

    /// <summary>A run that waits on a choice or a decision from the user.</summary>
    internal const string WorkflowsStatusWaitingForYou = "Waiting for you";

    /// <summary>A run that stopped itself (used its tries, ran out of allowance, timed out) and needs the user.</summary>
    internal const string WorkflowsStatusPaused = "Paused, needs you";

    /// <summary>A run the user took over.</summary>
    internal const string WorkflowsStatusTookOver = "You took over";

    /// <summary>A run that has not started.</summary>
    internal const string WorkflowsStatusNotStarted = "Not started";

    /// <summary>A run that finished.</summary>
    internal const string WorkflowsStatusDone = "Done";

    /// <summary>A run that failed.</summary>
    internal const string WorkflowsStatusFailed = "Failed";

    /// <summary>A run that ended without resolving what it was asked (the attention tone).</summary>
    internal const string WorkflowsStatusDidntWorkOut = "Didn't work out";

    /// <summary>A run the user cancelled.</summary>
    internal const string WorkflowsStatusCancelled = "Cancelled";

    /// <summary>The board's first lane (a stopgap table until the engine serves stage names).</summary>
    internal const string WorkflowsLaneScoping = "Scoping";

    /// <summary>The board's second lane.</summary>
    internal const string WorkflowsLaneDesign = "Design";

    /// <summary>The board's third lane.</summary>
    internal const string WorkflowsLanePlan = "Plan";

    /// <summary>The board's fourth lane.</summary>
    internal const string WorkflowsLaneBuild = "Build and prove";

    /// <summary>The board's fifth lane.</summary>
    internal const string WorkflowsLaneMerge = "Merge";

    /// <summary>The board's last lane.</summary>
    internal const string WorkflowsLaneDone = "Done";

    /// <summary>An empty lane's first line.</summary>
    internal const string WorkflowsLaneEmpty = "Nothing here.";

    /// <summary>An empty lane's second line.</summary>
    internal const string WorkflowsLaneEmptyBody = "No runs in this stage.";

    /// <summary>What an empty Merge lane teaches.</summary>
    internal const string WorkflowsLaneMergeEmpty = "Runs wait here for CI and docs, then for your approval.";

    /// <summary>An empty Done lane.</summary>
    internal const string WorkflowsLaneDoneEmpty = "No finished runs yet.";

    /// <summary>The view switch's board segment.</summary>
    internal const string WorkflowsViewBoard = "Board";

    /// <summary>The view switch's list segment.</summary>
    internal const string WorkflowsViewList = "List";

    /// <summary>The filter segment that shows every run.</summary>
    internal const string WorkflowsFilterAll = "All";

    /// <summary>The filter segment of runs waiting for the user.</summary>
    internal const string WorkflowsFilterNeedsYou = "Needs you";

    /// <summary>The filter segment of the user's own runs.</summary>
    internal const string WorkflowsFilterMine = "Mine";

    /// <summary>The search box's placeholder.</summary>
    internal const string WorkflowsSearchRuns = "Search runs";

    /// <summary>The keys the page answers to, beside the toolbar.</summary>
    internal const string WorkflowsKeyHints = "J K move, Enter open, V board or list";

    /// <summary>The empty box's first line.</summary>
    internal const string WorkflowsNothingHere = "Nothing here.";

    /// <summary>The empty box's second line while a filter hides the runs.</summary>
    internal const string WorkflowsNoRunsMatch = "No runs match this filter.";

    /// <summary>How long a run has been in its stage.</summary>
    /// <param name="age">The time in stage.</param>
    /// <returns>"in stage 1h 30m".</returns>
    internal static string WorkflowsInStage(TimeSpan age) => "in stage " + Age(age);

    /// <summary>How long ago a run ended.</summary>
    /// <param name="age">The time since it ended.</param>
    /// <returns>"ended 5h 20m ago".</returns>
    internal static string WorkflowsEndedAgo(TimeSpan age) => "ended " + Age(age) + " ago";

    /// <summary>The line under the board for open runs that no lane holds.</summary>
    /// <param name="count">How many.</param>
    /// <returns>"2 runs not on the board — switch to list".</returns>
    internal static string WorkflowsOffBoard(int count) => (count == 1 ? "1 run" : string.Create(CultureInfo.InvariantCulture, $"{count} runs")) + " not on the board — switch to list";
}
