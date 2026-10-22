using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the run canvas (F3 spec 3 §6). Never a served sentence.</summary>
internal static partial class Labels
{
    /// <summary>The words of a run's states.</summary>
    internal const string RunWordDone = "Done";

    /// <summary>Failed.</summary>
    internal const string RunWordFailed = "Failed";

    /// <summary>The run ended without a result.</summary>
    internal const string RunWordDidntWorkOut = "Didn't work out";

    /// <summary>Cancelled.</summary>
    internal const string RunWordCancelled = "Cancelled";

    /// <summary>A decision is open.</summary>
    internal const string RunWordWaiting = "Waiting for you";

    /// <summary>Parked or escalated.</summary>
    internal const string RunWordPaused = "Paused, needs you";

    /// <summary>Taken over.</summary>
    internal const string RunWordTookOver = "You took over";

    /// <summary>Running.</summary>
    internal const string RunWordRunning = "Running";

    /// <summary>Not started.</summary>
    internal const string RunWordNotStarted = "Not started";

    /// <summary>The line under the canvas while the engine does not serve branches and their labels.</summary>
    internal const string RunBranchesNotServed = "Branches and their labels aren't shown yet.";

    /// <summary>The pill on a loop edge.</summary>
    /// <param name="cap">How many times the loop may run.</param>
    /// <returns>"again, up to 4 times".</returns>
    internal static string RunLoopPill(int cap) => string.Create(CultureInfo.InvariantCulture, $"again, up to {cap} times");

    /// <summary>The pill on an edge, from the outcome it leaves on; an outcome this build does not know shows as served.</summary>
    /// <param name="outcome">The outcome.</param>
    /// <returns>"scope approved".</returns>
    internal static string EdgeLabel(string outcome) => _edgeLabels.TryGetValue(outcome, out var label) ? label : outcome;

    // Provisional until the engine serves a label for every transition (spec 3 §6); a test pins that none leaks an internal term.
    private static readonly Dictionary<string, string> _edgeLabels = new(StringComparer.Ordinal)
    {
        ["scoped"] = "scope approved",
        ["designed"] = "design approved",
        ["planned"] = "plan approved",
        ["resolved"] = "merged",
        ["unresolved"] = "could not finish",
        ["redesign"] = "needs a redesign",
        ["cancelled"] = "stopped",
    };
}
