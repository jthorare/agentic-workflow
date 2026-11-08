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
    public static IReadOnlyList<ServiceSchedule> From(SindriProfile profile, DateTimeOffset start) => Named(profile, start, NoRuns);

    /// <summary>
    /// The schedules for the services an approved profile names and for the build's built-in services (engine spec §7.2
    /// S1-D8): a built-in runs on its own cadence unless the profile names it, and runs with no approved profile too.
    /// </summary>
    /// <param name="profile">The approved profile, or null when none is approved.</param>
    /// <param name="builtIns">The services the build runs without a profile entry.</param>
    /// <param name="start">When the process started its scheduler.</param>
    /// <returns>One schedule per service: the profile's first, then the built-ins it doesn't name.</returns>
    public static IReadOnlyList<ServiceSchedule> From(SindriProfile? profile, IEnumerable<BuiltInService> builtIns, DateTimeOffset start) => From(profile, builtIns, start, NoRuns);

    /// <summary>
    /// The schedules as <see cref="From(SindriProfile?, IEnumerable{BuiltInService}, DateTimeOffset)"/> builds them, each
    /// first slot anchored on the service's latest recorded run in <paramref name="lastRuns"/> (see <see cref="FirstSlot"/>).
    /// </summary>
    /// <param name="profile">The approved profile, or null when none is approved.</param>
    /// <param name="builtIns">The services the build runs without a profile entry.</param>
    /// <param name="now">The current time.</param>
    /// <param name="lastRuns">The slot of each service's latest recorded run; a service missing here has none.</param>
    /// <returns>One schedule per service.</returns>
    public static IReadOnlyList<ServiceSchedule> From(SindriProfile? profile, IEnumerable<BuiltInService> builtIns, DateTimeOffset now, IReadOnlyDictionary<ServiceName, DateTimeOffset> lastRuns) =>
        FromRuns(profile, builtIns, now, lastRuns);

    /// <summary>
    /// Where a service's series starts when the schedule is (re)built: now when it has no recorded run or its next slot
    /// since the last run is past (one catch-up run, never a burst); else the slot after its last run, so a restart or a
    /// re-approval doesn't run it again early.
    /// </summary>
    /// <param name="lastRun">The slot of its latest recorded run, or null.</param>
    /// <param name="cadence">Its current cadence.</param>
    /// <param name="now">The current time.</param>
    /// <returns>The first slot.</returns>
    public static DateTimeOffset FirstSlot(DateTimeOffset? lastRun, Cadence cadence, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(cadence);
        if (lastRun is not { } last || last + cadence.Interval <= now)
        {
            return now;
        }

        return Min(last + cadence.Interval, now + cadence.Interval);   // a last run ahead of the clock (it stepped back) can't push the series out more than one period
    }

    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a <= b ? a : b;

    private static IReadOnlyDictionary<ServiceName, DateTimeOffset> NoRuns { get; } = new Dictionary<ServiceName, DateTimeOffset>();

    private static IReadOnlyList<ServiceSchedule> FromRuns(SindriProfile? profile, IEnumerable<BuiltInService> builtIns, DateTimeOffset now, IReadOnlyDictionary<ServiceName, DateTimeOffset> lastRuns)
    {
        ArgumentNullException.ThrowIfNull(builtIns);
        ArgumentNullException.ThrowIfNull(lastRuns);
        var named = profile is null ? [] : Named(profile, now, lastRuns);
        return [.. named, .. builtIns.Where(b => named.All(n => n.Name != b.Name)).Select(b => Anchored(b.Name, b.Cadence, now, lastRuns))];
    }

    private static IReadOnlyList<ServiceSchedule> Named(SindriProfile profile, DateTimeOffset now, IReadOnlyDictionary<ServiceName, DateTimeOffset> lastRuns) =>
        [.. (profile.Services ?? SindriProfile.NoServices).Select(s => Anchored(ServiceName.From(s.Key), Cadence.Parse(s.Value.Every).Value, now, lastRuns))];

    private static ServiceSchedule Anchored(ServiceName name, Cadence cadence, DateTimeOffset now, IReadOnlyDictionary<ServiceName, DateTimeOffset> lastRuns) =>
        new(name, cadence, FirstSlot(lastRuns.TryGetValue(name, out var last) ? last : null, cadence, now));
}
