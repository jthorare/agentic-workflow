using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Core.Scheduling;

/// <summary>A service's place in the schedule: its cadence and its next slot.</summary>
/// <param name="Name">The service.</param>
/// <param name="Cadence">How often it runs.</param>
/// <param name="NextSlot">When it runs next.</param>
public sealed record ServiceSchedule(ServiceName Name, Cadence Cadence, DateTimeOffset NextSlot)
{
    /// <summary>The schedule after the slot at <paramref name="now"/> ran: the next slot after now, never in the past (missed slots after a sleep are skipped, not replayed in a burst).</summary>
    /// <param name="now">The current time.</param>
    /// <returns>The advanced schedule.</returns>
    public ServiceSchedule Advance(DateTimeOffset now)
    {
        var next = NextSlot + Cadence.Interval;
        if (next <= now)
        {
            var missed = (long)Math.Floor((now - next) / Cadence.Interval) + 1;
            next += Cadence.Interval * missed;
        }

        return this with { NextSlot = next };
    }

    /// <summary>The schedules for the services an approved profile names, first slots at <paramref name="start"/>.</summary>
    /// <param name="profile">The approved profile (checked, so every name and cadence parses).</param>
    /// <param name="start">When the process started its scheduler.</param>
    /// <returns>One schedule per service.</returns>
    public static IReadOnlyList<ServiceSchedule> From(SindriProfile profile, DateTimeOffset start) =>
        [.. (profile.Services ?? SindriProfile.NoServices)
            .Select(s => new ServiceSchedule(ServiceName.From(s.Key), Cadence.Parse(s.Value.Every).Value, start))];
}
