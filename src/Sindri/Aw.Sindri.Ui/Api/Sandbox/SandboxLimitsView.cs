namespace Aw.Sindri.Ui.Api.Sandbox;

/// <summary>The limits a session runs under, as display-ready text (SandboxLimitsSummary).</summary>
/// <param name="MemoryMax">The per-session memory ceiling, for example '12GiB'.</param>
/// <param name="CpuQuota">The CPU quota, for example '400%'.</param>
/// <param name="TasksMax">The process limit.</param>
/// <param name="Wall">The wall-clock limit, for example '40m'; null (omitted) when none applies.</param>
internal sealed record SandboxLimitsView(
    string MemoryMax,
    string CpuQuota,
    int TasksMax,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? Wall = null);
