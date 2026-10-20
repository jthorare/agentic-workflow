namespace Aw.Context.Core.Review;

/// <summary>What a cleanup pass decided: after it the draft's review queue is empty, so the draft can be promoted.</summary>
/// <param name="Accepted">Entries accepted: the deterministic ones, the model ones at or above the keep line and those the agent kept.</param>
/// <param name="Removed">Model entries rejected: those the agent judged weak, and those it couldn't judge.</param>
/// <param name="Dropped">Orphaned overrides dropped.</param>
/// <param name="Mode">How the low-confidence entries were settled.</param>
public sealed record CleanupReport(int Accepted, int Removed, int Dropped, CleanupMode Mode);
