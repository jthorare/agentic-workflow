using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Sessions;

/// <summary>Where an Agents row's state, or one of its reasons, came from (Agents page spec §3.1): today's <see cref="StateSource"/> plus Meta and the engine.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AgentStateSource>))]
public enum AgentStateSource
{
    /// <summary>The aw:fleet hook's state file.</summary>
    [JsonStringEnumMemberName("hook")]
    Hook,

    /// <summary>Claude's own session record.</summary>
    [JsonStringEnumMemberName("record")]
    Record,

    /// <summary>Only the transcript's modification time.</summary>
    [JsonStringEnumMemberName("transcript")]
    Transcript,

    /// <summary>An <c lang="text">aw-meta</c> read: lane context, usage, merge queue, obligations, health or heavy slots.</summary>
    [JsonStringEnumMemberName("aw-meta")]
    AwMeta,

    /// <summary>The workflow engine's node state in the ledger.</summary>
    [JsonStringEnumMemberName("engine")]
    Engine,
}
