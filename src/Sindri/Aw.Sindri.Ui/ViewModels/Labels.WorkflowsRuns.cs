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
        ? string.Create(CultureInfo.InvariantCulture, $"{Math.Round(seconds, MidpointRounding.AwayFromZero)} s")
        : string.Create(CultureInfo.InvariantCulture, $"{(int)(seconds / 60)}m {(int)Math.Round(seconds % 60, MidpointRounding.AwayFromZero)}s");

    /// <summary>A moment as the Workflows pages write it: "Wed 31 Dec 2025 · 23:43" (the run row's tip, a run bar's tip and the Compare head all use it).</summary>
    /// <param name="local">The moment, in the viewer's zone.</param>
    /// <param name="seconds">True to add the seconds.</param>
    /// <returns>The words.</returns>
    internal static string WfDayTime(DateTimeOffset local, bool seconds = false) =>
        local.ToString("ddd d MMM yyyy", CultureInfo.InvariantCulture) + " \u00b7 " + local.ToString(seconds ? "HH:mm:ss" : "HH:mm", CultureInfo.InvariantCulture);

    /// <summary>The runs table's count: "500 runs".</summary>
    /// <param name="total">The runs.</param>
    /// <param name="more">True when older runs exist that were not read: "50+ runs".</param>
    /// <returns>The words.</returns>
    internal static string WfRunsCount(int total, bool more = false) => string.Create(CultureInfo.InvariantCulture, $"{total}{(more ? "+" : string.Empty)} {(total == 1 && !more ? "run" : "runs")}");

    /// <summary>The runs table's count with a filter on: "23 of 500 runs".</summary>
    /// <param name="shown">The runs the filters keep.</param>
    /// <param name="total">All the runs read.</param>
    /// <param name="more">True when older runs exist that were not read: "23 of the newest 50 runs".</param>
    /// <returns>The words.</returns>
    internal static string WfRunsCountOf(int shown, int total, bool more = false) =>
        more ? string.Create(CultureInfo.InvariantCulture, $"{shown} of the newest {total} runs") : string.Create(CultureInfo.InvariantCulture, $"{shown} of {total} {(total == 1 ? "run" : "runs")}");

    /// <summary>When a filter leaves no run.</summary>
    /// <param name="newest">How many runs were read when older ones exist too, else null.</param>
    /// <returns>The words.</returns>
    internal static string WfRunsNoMatch(int? newest) => newest is { } n ? string.Create(CultureInfo.InvariantCulture, $"No runs match these filters in the newest {n}.") : "No runs match these filters.";

    /// <summary>The Compare button: "Compare 2".</summary>
    /// <param name="count">The runs ticked.</param>
    /// <returns>The words.</returns>
    internal static string WfCompare(int count) => string.Create(CultureInfo.InvariantCulture, $"Compare {count}");
}
