namespace Aw.Sindri.Features.SweepLostSessions;

/// <summary>What the idle sweep knows about one Step session at one moment.</summary>
/// <param name="Now">The time now, from the sweep's clock.</param>
/// <param name="TurnEnded">When the Stop hook last touched the launch's turn-ended file; null while a turn runs.</param>
/// <param name="TurnFailed">When the StopFailure hook last touched the launch's turn-failed file (an API error ended the turn); null otherwise.</param>
/// <param name="SnapshotIdleSince">When the session collector's snapshot last saw the session turn idle; null while it is running or unknown.</param>
/// <param name="ToolRunning">Whether a tool call is in flight (the PreToolUse hook's marker, removed by PostToolUse).</param>
/// <param name="Nudged">When the sweep sent the one re-prompt; null when it has not.</param>
public sealed record IdleInputs(DateTimeOffset Now, DateTimeOffset? TurnEnded, DateTimeOffset? TurnFailed, DateTimeOffset? SnapshotIdleSince, bool ToolRunning, DateTimeOffset? Nudged);
