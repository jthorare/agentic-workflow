using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Events;

/// <summary>A limit that was reached without ending the session (sandbox spec §3.3).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<LimitHitKind>))]
public enum LimitHitKind
{
    /// <summary>TasksMax reached; forks fail with EAGAIN. Once per run.</summary>
    [JsonStringEnumMemberName("pids")]
    Pids,

    /// <summary>MemoryHigh crossed.</summary>
    [JsonStringEnumMemberName("memory")]
    Memory,

    /// <summary>Disk growth over the warning size.</summary>
    [JsonStringEnumMemberName("disk")]
    Disk,
}
