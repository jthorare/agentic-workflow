using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Linear;

/// <summary>What the pickers need of one team (spec 2026-11-04 §4.2).</summary>
/// <param name="TeamId">The team's Linear id; null for a picked team no tick has synced yet.</param>
/// <param name="TeamKey">The team's key, 'ENG'.</param>
/// <param name="States">Its workflow states.</param>
/// <param name="Cycles">Its current cycle, the next one and the last three.</param>
/// <param name="Members">Its members.</param>
/// <param name="Labels">Its labels.</param>
/// <param name="Name">The team's display name ('Engineering'); null for a team not synced yet or stored before the name was kept.</param>
/// <param name="SyncedAt">When this team's lists were last replaced; null when never.</param>
public sealed record LinearTeamLookups(
    ExternalId? TeamId,
    string TeamKey,
    IReadOnlyList<LinearLookupState> States,
    IReadOnlyList<LinearLookupCycle> Cycles,
    IReadOnlyList<LinearLookupMember> Members,
    IReadOnlyList<LinearLookupLabel> Labels,
    string? Name,
    DateTimeOffset? SyncedAt);
