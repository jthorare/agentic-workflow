using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Features.RecordHeartbeat;

/// <summary>Record one heartbeat for a scheduled slot.</summary>
/// <param name="Run">The scheduled run (grant and slot).</param>
public sealed record RecordHeartbeatRequest(ScheduledRun Run);
