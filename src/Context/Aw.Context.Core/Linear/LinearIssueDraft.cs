using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Linear;

/// <summary>A new issue. Every reference is an id the caller already resolved from the team's lookups; Context checks none of them against a team or an owner, Linear does.</summary>
/// <param name="TeamId">The team's Linear id.</param>
/// <param name="Title">The title, 1 to 255 characters.</param>
/// <param name="Description">The description as markdown, up to 65,536 characters, or null.</param>
/// <param name="StateId">The workflow state's Linear id, or null for the team's default.</param>
/// <param name="AssigneeId">The assignee's Linear user id, or null for none.</param>
/// <param name="Priority">0 (none) to 4 (low), or null for Linear's default.</param>
/// <param name="Estimate">The estimate, or null.</param>
/// <param name="CycleId">The cycle's Linear id, or null.</param>
/// <param name="ProjectId">The project's Linear id, or null.</param>
/// <param name="ParentId">The parent issue's Linear id, or null.</param>
/// <param name="LabelIds">The label ids; null or empty for none.</param>
public sealed record LinearIssueDraft(
    ExternalId TeamId,
    string Title,
    string? Description = null,
    ExternalId? StateId = null,
    ExternalId? AssigneeId = null,
    int? Priority = null,
    double? Estimate = null,
    ExternalId? CycleId = null,
    ExternalId? ProjectId = null,
    ExternalId? ParentId = null,
    IReadOnlyList<ExternalId>? LabelIds = null);
