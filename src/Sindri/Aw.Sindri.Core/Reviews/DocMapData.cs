namespace Aw.Sindri.Core.Reviews;

/// <summary>The docMap panel.</summary>
/// <param name="Claims">The PR's Covers: refs.</param>
/// <param name="Doc">The claimed doc's tree; null when none.</param>
/// <param name="Suggested">Docs suggested by issue or branch (none yet).</param>
/// <param name="ProjectDocs">The project's docs, offered when the PR names none.</param>
public sealed record DocMapData(IReadOnlyList<ReviewPageClaim> Claims, ReviewPageDoc? Doc, IReadOnlyList<ReviewPageProjectDoc> Suggested, IReadOnlyList<ReviewPageProjectDoc> ProjectDocs) : ReviewPanelData;
