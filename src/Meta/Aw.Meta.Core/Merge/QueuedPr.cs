namespace Aw.Meta.Core.Merge;

/// <summary>A pull request a lane said is ready to merge.</summary>
/// <param name="Pr">The pull request number.</param>
/// <param name="From">The lane that said so.</param>
/// <param name="Gate">The latest gate verdict for it (Pass, Fail or Conflict), or "none" when it was never gated.</param>
/// <param name="Github">What GitHub's merge queue says ("queued 2 (AWAITING_CHECKS)", or "queued (unknown)" when the read failed) for a pull request merge-run handed to it; null for any other.</param>
public sealed record QueuedPr(int Pr, string From, string Gate, string? Github = null);
