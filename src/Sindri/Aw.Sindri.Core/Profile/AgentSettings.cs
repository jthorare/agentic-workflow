using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>
/// The profile's 'agents:' block (screens spec E-28, launcher L-11, L-14): one <see cref="AgentLimits"/> per agent id, and the
/// reserved sibling key 'softLimit'. Left out of the canonical JSON when absent. In the file, 'softLimit' sits beside the
/// ids; it can't be an id because an id is lowercase only (see AgentId). Ids are checked by pattern, never against the catalog.
/// </summary>
/// <param name="SoftLimit">The fraction of a daily budget that raises the soft warning (default 0.8; 0 &lt; x &lt;= 1).</param>
/// <param name="Targets">The limits by agent id.</param>
[JsonConverter(typeof(AgentSettingsJsonConverter))]
public sealed record AgentSettings(decimal? SoftLimit = null, IReadOnlyDictionary<string, AgentLimits>? Targets = null)
{
    /// <summary>The key of <see cref="SoftLimit"/> in the file.</summary>
    public const string SoftLimitKey = "softLimit";

    /// <summary>The soft limit when the profile sets none.</summary>
    public const decimal DefaultSoftLimit = 0.8m;

    /// <summary>The hashes the approved profile lists for an id under agents.&lt;id&gt;.stepArgvHash (SL8).</summary>
    /// <param name="id">The agent id.</param>
    /// <returns>The hashes; empty when none are listed.</returns>
    public IReadOnlyList<string> StepArgvHashes(Launch.AgentId id) =>
        Targets is { } targets && targets.TryGetValue(id.Value, out var limits) ? limits.StepArgvHash ?? [] : [];
}
