using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Scheduling;

/// <summary>
/// The scheduler's own next slot per service, kept in memory for the services listing. The scheduler publishes it after each
/// refresh and each pass over the due slots; while it holds the lease a slot that comes due is run or skipped and moved on within
/// a tick, so a live slot that stays in the past means the slot was really missed.
/// </summary>
public sealed class LiveSchedule
{
    private IReadOnlyDictionary<ServiceName, DateTimeOffset>? _slots;

    /// <summary>The next slot of each scheduled service, or null before the scheduler has loaded a schedule.</summary>
    public IReadOnlyDictionary<ServiceName, DateTimeOffset>? Current => Volatile.Read(ref _slots);

    /// <summary>Replaces the live slots with those of <paramref name="schedules"/>.</summary>
    /// <param name="schedules">The scheduler's current schedules.</param>
    public void Publish(IReadOnlyList<ServiceSchedule> schedules)
    {
        ArgumentNullException.ThrowIfNull(schedules);
        Volatile.Write(ref _slots, schedules.ToDictionary(s => s.Name, s => s.NextSlot));
    }
}
