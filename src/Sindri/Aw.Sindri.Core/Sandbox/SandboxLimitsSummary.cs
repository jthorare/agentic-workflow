using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Sandbox;

/// <summary>The limits a session runs under, as display-ready text.</summary>
/// <param name="MemoryMax">The per-session memory ceiling, for example '12GiB'.</param>
/// <param name="CpuQuota">The CPU quota, for example '400%'.</param>
/// <param name="TasksMax">The process limit.</param>
/// <param name="Wall">The wall-clock limit, or null when none applies (an attended session).</param>
public sealed record SandboxLimitsSummary(
    string MemoryMax,
    string CpuQuota,
    int TasksMax,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Wall = null);
