using System.Text.Json.Serialization;
using Aw.Meta.Core.Health;
using Aw.Meta.Core.Lanes;

namespace Aw.Meta.Features.Health;

/// <summary>What the health check found.</summary>
/// <param name="Groups">Processes by kind with their memory.</param>
/// <param name="ProcessCount">How many processes run.</param>
/// <param name="Host">Memory, swap and disk.</param>
/// <param name="Orphans">The orphans found (Temporal dev servers left behind).</param>
/// <param name="Killed">The pids of the orphans terminated by this run.</param>
/// <param name="Duplicates">MCP server commands running more often than there are sessions to own them.</param>
/// <param name="MissingLanes">Configured lanes with no live session; null when there is no team config to check against.</param>
/// <param name="HeavyLocks">The taken heavy-job lock slots.</param>
/// <param name="MemoryLow">Whether memory is low by the team's thresholds (available memory under resources.memFloorGb; swap does not count; the defaults without a team config).</param>
/// <param name="Lanes">How each non-Claude lane stands; absent (null) for a team of Claude lanes.</param>
/// <param name="Heavy">The heavy-slot reservation the meta computed for the merge queue, with its staleness; null when none was computed or the team turned it off.</param>
/// <param name="Builders">The builders per lane to run now.</param>
/// <param name="Context">Each lane's context size; null when there is no team config or it could not be read.</param>
/// <param name="Attention">Whether something needs acting on: an orphan still running, a missing lane or low memory.</param>
public sealed record HealthResponse(
    IReadOnlyList<ProcessGroup> Groups,
    int ProcessCount,
    HostResources Host,
    IReadOnlyList<ProcessInfo> Orphans,
    IReadOnlyList<int> Killed,
    IReadOnlyList<DuplicateServer> Duplicates,
    IReadOnlyList<string>? MissingLanes,
    IReadOnlyList<HeavyLock> HeavyLocks,
    bool MemoryLow,
    bool Attention,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<LaneHealth>? Lanes = null,
    HeavySlots? Heavy = null,
    BuilderAdvice? Builders = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<LaneContext>? Context = null)
{
    /// <summary>Whether a beating watch consumes the meta's mail; null when not computed.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Consumer { get; init; }

    /// <summary>The warning when systemd's DefaultOOMPolicy is known and is not continue (issue #2931); null otherwise. Never raises attention.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OomPolicy { get; init; }

    /// <summary>The warning when the installed heavy or meta-gate-reserve differs from the toolkit checkout's (issue #3698); null otherwise. Never raises attention.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StaleScripts { get; init; }
}
