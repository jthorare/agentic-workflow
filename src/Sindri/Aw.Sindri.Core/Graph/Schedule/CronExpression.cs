using System.Diagnostics.CodeAnalysis;

namespace Aw.Sindri.Core.Graph.Schedule;

/// <summary>
/// A five-field cron schedule (minute hour day-of-month month day-of-week) read in an IANA timezone. A day-of-month and a day-of-week that are both
/// restricted match on either (the Vixie rule). A local time a spring-forward skips fires once, at the first valid instant after it; a local time a
/// fall-back repeats fires once, at its first occurrence.
/// </summary>
public sealed class CronExpression
{
    private CronExpression()
    {
    }

    /// <summary>Reads <paramref name="cron"/> in <paramref name="timezone"/> (null is UTC).</summary>
    /// <param name="cron">Five fields: <c language="text">*</c>, lists, ranges, steps, 0-7 for Sunday, MON and JAN names.</param>
    /// <param name="timezone">An IANA id such as America/Los_Angeles, or null for UTC.</param>
    /// <param name="expression">The schedule; null when the text is not valid.</param>
    /// <param name="error">Which field or timezone is wrong and the fix; null when valid.</param>
    /// <returns>True when the schedule reads.</returns>
    public static bool TryParse(string cron, string? timezone, [NotNullWhen(true)] out CronExpression? expression, [NotNullWhen(false)] out string? error) =>
        throw new NotSupportedException();

    /// <summary>The first tick strictly after <paramref name="after"/>.</summary>
    /// <param name="after">The instant to start after.</param>
    /// <returns>The tick, as an instant.</returns>
    public DateTimeOffset Next(DateTimeOffset after) => throw new NotSupportedException();

    /// <summary>The latest tick <c language="text">t</c> with <paramref name="after"/> &lt; t &lt;= <paramref name="upTo"/> (a missed schedule runs once).</summary>
    /// <param name="after">The last instant already handled.</param>
    /// <param name="upTo">The newest instant to consider, included.</param>
    /// <returns>The tick, or null when none fell in the window.</returns>
    public DateTimeOffset? LastTick(DateTimeOffset after, DateTimeOffset upTo) => throw new NotSupportedException();
}
