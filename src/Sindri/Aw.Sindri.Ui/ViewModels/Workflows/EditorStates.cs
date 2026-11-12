using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The state words of the workflow editor (spec 4.8). One place, so a word never drifts between the header, the footer and a toast.</summary>
internal static class EditorStates
{
    /// <summary>The line shown for a write refused while a test runs.</summary>
    internal const string TestRunningRefusal = "A test is running. Esc cancels it";

    /// <summary>The line for a stale base.</summary>
    internal const string Conflict = "Changed elsewhere. Reload";

    /// <summary>The chip of a revision with no test.</summary>
    internal const string NotTested = "Not tested";

    /// <summary>The chip of a revision whose newest test run passed.</summary>
    internal const string TestedChip = "Tested";

    /// <summary>The chip of a workflow that is on.</summary>
    internal const string ActiveChip = "Active";

    /// <summary>The Activate confirm's question for an untested revision.</summary>
    internal const string ActivateUntested = "Not tested yet. Activate anyway?";

    /// <summary>The line for a cancelled test.</summary>
    internal const string TestCancelled = "Test cancelled";

    /// <summary>The gallery's failed line.</summary>
    internal const string TemplatesFailed = "Couldn't load templates. Retry";

    /// <summary>The header's primary button: the one next step. Engine down wins over everything, then a conflict, then a running test.</summary>
    /// <param name="state">The editor's state.</param>
    /// <returns>The button's words.</returns>
    internal static string Primary(EditorSnapshot state) =>
        state.EngineDown ? "Start Sindri"
        : state.Conflict ? "Reload"
        : state.Testing ? "Testing..."
        : !state.HasTrigger ? "Add a trigger"
        : !state.HasAction ? "Add an action"
        : state.Findings > 0 ? "Fix " + Things(state.Findings)
        : !state.Tested ? "Test run"
        : "Activate";

    /// <summary>The header's count of findings: "2 things to fix".</summary>
    /// <param name="count">The findings.</param>
    /// <returns>The words.</returns>
    internal static string ThingsToFix(int count) => Things(count) + " to fix";

    /// <summary>The checklist's heading after a template is picked: "1 thing left".</summary>
    /// <param name="count">The things left.</param>
    /// <returns>The words.</returns>
    internal static string ThingsLeft(int count) => Things(count) + " left";

    /// <summary>The line when the event stream is down.</summary>
    /// <param name="lastRead">The time of the last read, as the page shows it ("14:02").</param>
    /// <returns>The words.</returns>
    internal static string NotLive(string lastRead) => "Not live. Last read " + lastRead;

    private static string Things(int count) => string.Create(CultureInfo.InvariantCulture, $"{count} {(count == 1 ? "thing" : "things")}");
}
