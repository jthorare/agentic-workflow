using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Events;

/// <summary>How a launched session ended (API spec §8.1).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SessionEndReason>))]
public enum SessionEndReason
{
    /// <summary>'session wrap' reported the agent's exit.</summary>
    [JsonStringEnumMemberName("exit")]
    Exit,

    /// <summary>No heartbeat for 120 s.</summary>
    [JsonStringEnumMemberName("lost")]
    Lost,

    /// <summary>A sandbox limit killed the session (memory or wall); 'session.ended' names the kind, the limit and the peak. The engine maps it to step.failed(limit).</summary>
    [JsonStringEnumMemberName("limit")]
    Limit,
}
