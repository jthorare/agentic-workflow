namespace Aw.Meta.Core.Github;

/// <summary>What a CI gate reads about a pull request: `gh pr view --json headRefOid,baseRefName,reviewDecision,mergeStateStatus`.</summary>
/// <param name="Head">The head commit sha (headRefOid).</param>
/// <param name="Base">The base branch (baseRefName).</param>
/// <param name="ReviewDecision">APPROVED, CHANGES_REQUESTED, REVIEW_REQUIRED, or empty when the base asks for no review.</param>
/// <param name="MergeState">CLEAN, BLOCKED, BEHIND, DIRTY, UNSTABLE, HAS_HOOKS, DRAFT or UNKNOWN.</param>
public sealed record PrCiState(string Head, string Base, string ReviewDecision, string MergeState);
