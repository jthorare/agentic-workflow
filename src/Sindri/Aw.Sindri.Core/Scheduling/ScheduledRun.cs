using Aw.Sindri.Core.Lease;

namespace Aw.Sindri.Core.Scheduling;

/// <summary>One run of a background service: the lease it runs under and the slot it fills.</summary>
/// <param name="Grant">The process's lease grant; every write the run makes is fenced by it.</param>
/// <param name="Slot">The scheduled time of this run; it keys the run's events, so a replayed slot writes nothing twice.</param>
public sealed record ScheduledRun(LeaseGrant Grant, DateTimeOffset Slot);
