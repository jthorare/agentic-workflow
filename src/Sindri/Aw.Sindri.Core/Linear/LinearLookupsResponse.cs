
namespace Aw.Sindri.Core.Linear;

/// <summary>What the filter bar and the pickers offer for one team (Linear hub spec 2026-11-04 §3). A team never synced answers null <paramref name="SyncedAt"/> and empty lists.</summary>
/// <param name="Team">The team.</param>
/// <param name="SyncedAt">When the lists were last stored; null before the first sync.</param>
/// <param name="States">The states, in board order.</param>
/// <param name="Members">The members.</param>
/// <param name="Labels">The labels.</param>
/// <param name="Cycles">The cycles.</param>
public sealed record LinearLookupsResponse(
    LinearTeamRef Team,
    DateTimeOffset? SyncedAt,
    IReadOnlyList<LinearStateOption> States,
    IReadOnlyList<LinearMemberOption> Members,
    IReadOnlyList<LinearLabelOption> Labels,
    IReadOnlyList<LinearCycleOption> Cycles);
