using Aw.Meta.Core.Watch;

namespace Aw.Meta.Core.Github;

/// <summary>One pull request as `gh pr view --json` reports it; a field not asked for keeps its default.</summary>
/// <param name="Head">The head commit sha (headRefOid).</param>
/// <param name="HeadRef">The head branch (headRefName).</param>
/// <param name="IsDraft">Whether it is a draft.</param>
/// <param name="Title">The title.</param>
/// <param name="State">Open, closed or merged.</param>
/// <param name="EndedAt">When it merged or closed (mergedAt, closedAt); null while open, when not asked for, or when GitHub gave none.</param>
public sealed record PrDetails(string Head, string HeadRef, bool IsDraft, string Title, PrState State, DateTimeOffset? EndedAt = null);
