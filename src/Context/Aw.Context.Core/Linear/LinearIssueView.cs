using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Linear;

/// <summary>One issue as Linear has it now, with the title, text and state name the stored <see cref="LinearIssueFields"/> leave out. A live read and a write's answer both give one.</summary>
/// <param name="Id">The issue's Linear id.</param>
/// <param name="Key">The human key, 'ENG-12'.</param>
/// <param name="TeamKey">The team's key, 'ENG'.</param>
/// <param name="Title">The title.</param>
/// <param name="Description">The description as markdown, or null when it has none.</param>
/// <param name="StateId">The workflow state's Linear id.</param>
/// <param name="StateName">The state's name, 'In Review'.</param>
/// <param name="AssigneeId">The assignee's Linear user id, or null.</param>
/// <param name="Priority">0 (none) to 4 (low).</param>
/// <param name="Estimate">The estimate, or null.</param>
/// <param name="CycleId">The cycle's Linear id, or null.</param>
/// <param name="ProjectId">The project's Linear id, or null.</param>
/// <param name="ParentId">The parent issue's Linear id, or null.</param>
/// <param name="LabelIds">The label ids.</param>
/// <param name="UpdatedAt">When Linear last changed the issue.</param>
public sealed record LinearIssueView(
    ExternalId Id,
    LinearIssueKey Key,
    string TeamKey,
    string Title,
    string? Description,
    ExternalId StateId,
    string StateName,
    ExternalId? AssigneeId,
    int Priority,
    double? Estimate,
    ExternalId? CycleId,
    ExternalId? ProjectId,
    ExternalId? ParentId,
    IReadOnlyList<ExternalId> LabelIds,
    DateTimeOffset UpdatedAt);
