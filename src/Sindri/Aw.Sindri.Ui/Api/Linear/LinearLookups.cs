namespace Aw.Sindri.Ui.Api.Linear;

/// <summary>GET /sindri/linear/lookups/{team} (Linear hub spec §3): what the filter bar and pickers offer for one team. Owner only.</summary>
/// <param name="Team">The team.</param>
/// <param name="SyncedAt">When the lookups last synced; null (with empty lists) before the first sync.</param>
/// <param name="States">The states, in board order.</param>
/// <param name="Members">The members.</param>
/// <param name="Labels">The labels.</param>
/// <param name="Cycles">The cycles.</param>
internal sealed record LinearLookups(LinearTeam Team, DateTimeOffset? SyncedAt, IReadOnlyList<LinearStateOption> States, IReadOnlyList<LinearMember> Members, IReadOnlyList<LinearLabel> Labels, IReadOnlyList<LinearCycle> Cycles);
