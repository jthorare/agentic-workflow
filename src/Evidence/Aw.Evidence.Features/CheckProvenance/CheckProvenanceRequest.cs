namespace Aw.Evidence.Features.CheckProvenance;

/// <summary>Determines whether the local database holds seed data only; in a worktree with a stack slot, that slot's database.</summary>
/// <param name="Worktree">The git worktree whose slot database to check (UI_EVIDENCE_WORKTREE, else the working directory's checkout when it has a slot file), or null for the main stack's.</param>
public sealed record CheckProvenanceRequest(string? Worktree = null);
