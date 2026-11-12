using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>
/// The profile's 'agents:' block (screens spec E-28, launcher L-11, L-14): one <see cref="AgentLimits"/> per agent id, and the
/// reserved sibling key 'softLimit'. Left out of the canonical JSON when absent. In the file, 'softLimit' sits beside the
/// ids; it can't be an id because an id is lowercase only (see AgentId). Ids are checked by pattern, never against the catalog. The Agents page's three keys
/// ('idleOwedMinutes', 'slotWaitMinutes' and 'page') sit beside the ids the same way; 'page' is reserved, so no agent is called page.
/// </summary>
/// <param name="SoftLimit">The fraction of a daily budget that raises the soft warning (default 0.8; 0 &lt; x &lt;= 1).</param>
/// <param name="Targets">The limits by agent id.</param>
/// <param name="IdleOwedMinutes">The minutes a lane idles before owed work lists it in Needs you (1 to 1440, default 10; Agents page spec §3.2).</param>
/// <param name="SlotWaitMinutes">The minutes a heavy waiter waits before it is listed in Waiting (1 to 1440, default 30).</param>
/// <param name="Page">'rows' or 'legacy' (default 'legacy' until the new page ships; spec §8).</param>
[JsonConverter(typeof(AgentSettingsJsonConverter))]
public sealed record AgentSettings(decimal? SoftLimit = null, IReadOnlyDictionary<string, AgentLimits>? Targets = null, int? IdleOwedMinutes = null, int? SlotWaitMinutes = null, string? Page = null)
{
    /// <summary>The key of <see cref="SoftLimit"/> in the file.</summary>
    public const string SoftLimitKey = "softLimit";

    /// <summary>The soft limit when the profile sets none.</summary>
    public const decimal DefaultSoftLimit = 0.8m;

    /// <summary>The key of <see cref="IdleOwedMinutes"/> in the file.</summary>
    public const string IdleOwedMinutesKey = "idleOwedMinutes";

    /// <summary>The key of <see cref="SlotWaitMinutes"/> in the file.</summary>
    public const string SlotWaitMinutesKey = "slotWaitMinutes";

    /// <summary>The key of <see cref="Page"/> in the file.</summary>
    public const string PageKey = "page";

    /// <summary>The idle-owed minutes when the profile sets none.</summary>
    public const int DefaultIdleOwedMinutes = 10;

    /// <summary>The slot-wait minutes when the profile sets none.</summary>
    public const int DefaultSlotWaitMinutes = 30;

    /// <summary>The smallest value of either minutes key.</summary>
    public const int MinMinutes = 1;

    /// <summary>The largest value of either minutes key (one day).</summary>
    public const int MaxMinutes = 1440;

    /// <summary>The new Agents page is the default.</summary>
    public const string PageRows = "rows";

    /// <summary>The old views stay the default.</summary>
    public const string PageLegacy = "legacy";

    /// <summary>The hashes the approved profile lists for an id under agents.&lt;id&gt;.stepArgvHash (SL8).</summary>
    /// <param name="id">The agent id.</param>
    /// <returns>The hashes; empty when none are listed.</returns>
    public IReadOnlyList<string> StepArgvHashes(Launch.AgentId id) =>
        Targets is { } targets && targets.TryGetValue(id.Value, out var limits) ? limits.StepArgvHash ?? [] : [];
}
