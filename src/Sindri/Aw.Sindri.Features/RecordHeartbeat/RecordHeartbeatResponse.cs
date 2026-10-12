namespace Aw.Sindri.Features.RecordHeartbeat;

/// <summary>Where the heartbeat landed.</summary>
/// <param name="Sequence">Its ledger position.</param>
/// <param name="AlreadyRecorded">True when this slot was recorded before (a replay).</param>
public sealed record RecordHeartbeatResponse(long Sequence, bool AlreadyRecorded);
