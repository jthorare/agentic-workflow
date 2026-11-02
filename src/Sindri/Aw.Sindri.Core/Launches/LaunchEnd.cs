using System.Text.Json.Serialization;
using Aw.Sindri.Core.Events;

namespace Aw.Sindri.Core.Launches;

/// <summary>How a started launch's session ended ('session.ended').</summary>
/// <param name="Reason">Exit, lost or limit.</param>
/// <param name="ExitCode">The agent's exit code for an exit, otherwise null.</param>
/// <param name="At">When the end was recorded.</param>
/// <param name="Kind">Which limit ended the session, for a limit ending; left out otherwise.</param>
/// <param name="Limit">The limit's value (bytes for memory, seconds for wall); left out unless the reason is limit.</param>
/// <param name="Peak">The peak of that limit, in the same unit; left out unless the reason is limit.</param>
public sealed record LaunchEnd(
    SessionEndReason Reason,
    int? ExitCode,
    DateTimeOffset At,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] LimitKind? Kind = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? Limit = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? Peak = null);
