using System.Globalization;
using Aw.Kernel;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): a workflow's older runs and a run row's day and time (#3305). Never a served sentence.</summary>
internal static partial class Labels
{
    /// <summary>The runs list's button while Sindri says older runs exist.</summary>
    internal const string WfRunsShowOlder = "Show older runs";

    /// <summary>The same button while the next page is on the way.</summary>
    internal const string WfRunsLoadingOlder = "Loading older runs…";

    /// <summary>A failed Show older runs read: the reason, then the code.</summary>
    /// <param name="error">Why.</param>
    /// <returns>The sentence.</returns>
    internal static string WfRunsOlderFailed(Error error) => "Couldn't load older runs just now. " + InstanceRefused(error);

    /// <summary>
    /// A run row's time: its age under a day ("Ended 5m ago"); past a day the day and the clock time in local time, "Ended yesterday 07:00", "Ended Mar 1 07:00", or
    /// "Ended Nov 10 2025 07:00" in another year.
    /// </summary>
    /// <param name="running">True while it is live (it says when it started).</param>
    /// <param name="at">When it started (live) or ended.</param>
    /// <param name="clock">The clock, with the local time zone.</param>
    /// <returns>The words.</returns>
    internal static string WfRunWhen(bool running, DateTimeOffset at, TimeProvider clock)
    {
        var now = clock.GetUtcNow();
        if (now - at < TimeSpan.FromDays(1))
        {
            return JobWhen(running, now - at);
        }

        return (running ? "Started " : "Ended ") + WfRunDay(at, clock);
    }

    /// <summary>
    /// The table's time cell: the run's age under a day ("5m ago"), past a day the day and the clock time like <see cref="WfRunWhen"/> ("Yesterday 07:00", "Mar 1 07:00", "Nov 10 2025 07:00").
    /// </summary>
    /// <param name="at">When the run started.</param>
    /// <param name="clock">The clock, with the local time zone.</param>
    /// <returns>The words.</returns>
    internal static string WfRunTime(DateTimeOffset at, TimeProvider clock)
    {
        var age = clock.GetUtcNow() - at;
        if (age < TimeSpan.FromDays(1))
        {
            return AgoWords(age);
        }

        var day = WfRunDay(at, clock);
        return string.Concat(char.ToUpperInvariant(day[0]).ToString(), day.AsSpan(1));
    }

    // The day and the clock time in local time: "yesterday 07:00", "Mar 1 07:00", "Nov 10 2025 07:00". One wording for the row's name and the table's time cell.
    private static string WfRunDay(DateTimeOffset at, TimeProvider clock)
    {
        var (local, today) = (TimeZoneInfo.ConvertTime(at, clock.LocalTimeZone), TimeZoneInfo.ConvertTime(clock.GetUtcNow(), clock.LocalTimeZone).Date);
        var day = local.Date == today.AddDays(-1) ? "yesterday"
            : local.Year == today.Year ? local.ToString("MMM d", CultureInfo.InvariantCulture)
            : local.ToString("MMM d yyyy", CultureInfo.InvariantCulture);
        return day + " " + local.ToString("HH:mm", CultureInfo.InvariantCulture);
    }

    /// <summary>The exact moment, in local time, for a time's tip: "Mon 9 Mar 2026 · 07:00:00".</summary>
    /// <param name="at">The moment.</param>
    /// <param name="clock">The clock, with the local time zone.</param>
    /// <returns>The words.</returns>
    internal static string WfRunMoment(DateTimeOffset at, TimeProvider clock) =>
        TimeZoneInfo.ConvertTime(at, clock.LocalTimeZone).ToString("ddd d MMM yyyy · HH:mm:ss", CultureInfo.InvariantCulture);
}
