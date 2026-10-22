using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>The 'sandbox.limits' keys (sandbox spec §3.3, §9). A key left out takes its default (<see cref="SandboxDefaults"/>).</summary>
/// <param name="Memory">Per-session memory: 'auto' or a size such as '12GiB'.</param>
/// <param name="Cpu">Per-session CPU share, a percent such as '50%'.</param>
/// <param name="Tasks">Processes per session.</param>
/// <param name="Sessions">Concurrent sandboxed sessions.</param>
/// <param name="Total">The parent slice's memory cap: a percent or a size.</param>
/// <param name="WallGrace">Added to a Step's timeout (a duration).</param>
/// <param name="HomeTmpfs">The size of the $HOME tmpfs, a size.</param>
/// <param name="ParkMax">The longest single suspension the scope's ceiling allows (a duration).</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SandboxLimits(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Memory = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Cpu = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Tasks = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Sessions = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Total = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? WallGrace = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? HomeTmpfs = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ParkMax = null);
