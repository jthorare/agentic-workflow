using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Linear;

/// <summary>An issue's fields the text leaves out, as the 'linear_issue' side table keeps them (spec 2026-11-04 §4.1).</summary>
/// <param name="TeamId">The team's Linear id.</param>
/// <param name="Identifier">The issue's identifier, 'ENG-12'.</param>
/// <param name="StateId">The workflow state's Linear id.</param>
/// <param name="StateType">The state's type: triage, backlog, unstarted, started, completed or canceled.</param>
/// <param name="AssigneeId">The assignee's Linear user id, or null.</param>
/// <param name="Priority">0 (none) to 4 (low).</param>
/// <param name="Estimate">The estimate, or null.</param>
/// <param name="CycleId">The cycle's Linear id, or null.</param>
/// <param name="ProjectId">The project's Linear id, or null.</param>
/// <param name="ParentId">The parent issue's Linear id, or null.</param>
/// <param name="LabelIds">The label ids.</param>
/// <param name="DueDate">The due date as Linear spells it ('2026-11-10'), or null.</param>
/// <param name="SortOrder">Linear's sort order in its list.</param>
/// <param name="UpdatedAt">When Linear last changed the issue.</param>
public sealed record LinearIssueFields(
    ExternalId? TeamId,
    string Identifier,
    ExternalId? StateId,
    string? StateType,
    ExternalId? AssigneeId,
    int Priority,
    double? Estimate,
    ExternalId? CycleId,
    ExternalId? ProjectId,
    ExternalId? ParentId,
    IReadOnlyList<ExternalId> LabelIds,
    string? DueDate,
    double? SortOrder,
    DateTimeOffset UpdatedAt);
