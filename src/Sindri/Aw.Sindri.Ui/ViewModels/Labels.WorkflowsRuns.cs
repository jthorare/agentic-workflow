using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The words of the Workflows page's runs table and Compare (<see cref="Labels"/>, continued; #3683).</summary>
internal static partial class Labels
{
    /// <summary>The trigger of a run in words (#3683).</summary>
    /// <param name="kind">The served kind: manual, schedule, cli, agent.</param>
    /// <returns>"By hand", "On schedule", "From the CLI", "By an agent", else the kind as served, capitalised.</returns>
    internal static string WfRunTriggerWords(string kind) => kind switch
    {
        "manual" => "By hand",
        "schedule" => "On schedule",
        "cli" => "From the CLI",
        "agent" => "By an agent",
        _ => WfTriggerWord(kind),
    };

    /// <summary>An age to the minute for a tip: "22m", "7h 42m", "1d 2h".</summary>
    /// <param name="age">How long ago.</param>
    /// <returns>The words, ending in "ago".</returns>
    internal static string WfExactAge(TimeSpan age) => (age.TotalHours < 1
        ? string.Create(CultureInfo.InvariantCulture, $"{Math.Max(0, (int)age.TotalMinutes)}m")
        : age.TotalDays < 1
            ? string.Create(CultureInfo.InvariantCulture, $"{(int)age.TotalHours}h {age.Minutes}m")
            : string.Create(CultureInfo.InvariantCulture, $"{(int)age.TotalDays}d {age.Hours}h")) + " ago";

    /// <summary>A run's duration: "9 s", "1m 4s".</summary>
    /// <param name="seconds">How long it took.</param>
    /// <returns>The words.</returns>
    internal static string WfDuration(double seconds) => seconds < 60
        ? string.Create(CultureInfo.InvariantCulture, $"{Math.Round(seconds)} s")
        : string.Create(CultureInfo.InvariantCulture, $"{(int)(seconds / 60)}m {(int)Math.Round(seconds % 60)}s");

    /// <summary>The runs table's count: "500 runs".</summary>
    /// <param name="total">The runs.</param>
    /// <returns>The words.</returns>
    internal static string WfRunsCount(int total) => string.Create(CultureInfo.InvariantCulture, $"{total} {(total == 1 ? "run" : "runs")}");

    /// <summary>The runs table's count with a filter on: "23 of 500 runs".</summary>
    /// <param name="shown">The runs the filters keep.</param>
    /// <param name="total">All the runs read.</param>
    /// <returns>The words.</returns>
    internal static string WfRunsCountOf(int shown, int total) => string.Create(CultureInfo.InvariantCulture, $"{shown} of {total} {(total == 1 ? "run" : "runs")}");

    /// <summary>When a filter leaves no run.</summary>
    internal const string WfRunsNoMatch = "No runs match these filters.";

    /// <summary>The Compare button: "Compare 2".</summary>
    /// <param name="count">The runs ticked.</param>
    /// <returns>The words.</returns>
    internal static string WfCompare(int count) => string.Create(CultureInfo.InvariantCulture, $"Compare {count}");
}
