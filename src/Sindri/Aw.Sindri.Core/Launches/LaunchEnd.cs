using Aw.Sindri.Core.Events;

namespace Aw.Sindri.Core.Launches;

/// <summary>How a started launch's session ended ('session.ended').</summary>
/// <param name="Reason">Exit or lost.</param>
/// <param name="ExitCode">The agent's exit code for an exit, otherwise null.</param>
/// <param name="At">When the end was recorded.</param>
public sealed record LaunchEnd(SessionEndReason Reason, int? ExitCode, DateTimeOffset At);
