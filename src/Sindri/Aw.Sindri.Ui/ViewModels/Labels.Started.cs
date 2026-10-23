using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): who started a run and when. Never a served sentence.</summary>
internal static partial class Labels
{
    /// <summary>Who started a run, in words.</summary>
    /// <param name="startedBy">The served value ("human:cli", "session:...", "service:host").</param>
    /// <returns>"you", "an agent session", "Sindri", or the served value for any other.</returns>
    internal static string StartedByWords(string startedBy) => startedBy switch
    {
        _ when startedBy.StartsWith("human", StringComparison.Ordinal) => "you",
        "service:host" => "Sindri",
        _ when startedBy.StartsWith("service:", StringComparison.Ordinal) => "Sindri's " + startedBy["service:".Length..] + " service",
        _ when startedBy.StartsWith("session:", StringComparison.Ordinal) => "an agent session",
        _ => startedBy,
    };

    /// <summary>When a run started, as a card says it: the time today, the date and time on another day.</summary>
    /// <param name="startedAt">When, in the person's time zone.</param>
    /// <param name="now">Now, in the same time zone.</param>
    /// <returns>"11:02" or "Oct 11, 11:02".</returns>
    internal static string StartedClock(DateTimeOffset startedAt, DateTimeOffset now) =>
        startedAt.Date == now.Date
            ? startedAt.ToString("HH:mm", CultureInfo.InvariantCulture)
            : startedAt.ToString("MMM d, HH:mm", CultureInfo.InvariantCulture);

    /// <summary>Who took a run over and when.</summary>
    /// <param name="clock">When, as <see cref="StartedClock"/> says it.</param>
    /// <param name="takenOverBy">The actor, as served ("human:cli").</param>
    /// <returns>"Taken over by you at 11:04"; empty when nobody is recorded.</returns>
    internal static string RunTakenOver(string? clock, string? takenOverBy) => (clock, takenOverBy) switch
    {
        (null or "", null or "") => string.Empty,
        (null or "", { } who) => "Taken over by " + StartedByWords(who),
        ({ } at, null or "") => "Taken over at " + at,
        ({ } at, { } who) => "Taken over by " + StartedByWords(who) + " at " + at,
    };

    /// <summary>Who started a run and when; either part is left out when it is not served, and nothing says when neither is.</summary>
    /// <param name="clock">When it started, or null.</param>
    /// <param name="startedBy">Who started it, or null.</param>
    /// <returns>"Started 11:02 by you", "Started 11:02", "Started by you", or empty.</returns>
    internal static string WorkflowStarted(string? clock, string? startedBy) => (clock, startedBy) switch
    {
        (null or "", null or "") => string.Empty,
        (null or "", { } who) => "Started by " + StartedByWords(who),
        ({ } at, null or "") => "Started " + at,
        ({ } at, { } who) => "Started " + at + " by " + StartedByWords(who),
    };
}
