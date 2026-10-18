using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'session.ended' payload (API spec §8.1; sandbox spec §6, §8). Key 'session.ended:&lt;launch&gt;'.</summary>
/// <param name="Launch">The launch.</param>
/// <param name="Reason">Exit, lost or limit.</param>
/// <param name="ExitCode">The agent's exit code for <see cref="SessionEndReason.Exit"/>, otherwise null.</param>
/// <param name="Kind">Which limit ended the session, for <see cref="SessionEndReason.Limit"/>; left out otherwise.</param>
/// <param name="Limit">The limit's value (bytes for memory, seconds for wall); left out unless the reason is limit.</param>
/// <param name="Peak">The peak of that limit, in the same unit; left out unless the reason is limit.</param>
/// <param name="Peaks">The peaks of every limit, so the limits are tuned from data; left out when the session had no sandbox.</param>
public sealed record SessionEnded(
    LaunchId Launch,
    SessionEndReason Reason,
    int? ExitCode,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] LimitKind? Kind = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? Limit = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? Peak = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] LimitPeaks? Peaks = null)
{
    /// <summary>A session a sandbox limit ended: the reason is <see cref="SessionEndReason.Limit"/> and the limit's fields are all present.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="end">Which limit, its value and peak.</param>
    /// <returns>The payload.</returns>
    public static SessionEnded Limited(LaunchId launch, LimitEnd end)
    {
        ArgumentNullException.ThrowIfNull(end);
        return new SessionEnded(launch, SessionEndReason.Limit, ExitCode: null, end.Kind, end.Limit, end.Peak, end.Peaks);
    }
}
