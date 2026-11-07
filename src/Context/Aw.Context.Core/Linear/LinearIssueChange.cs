using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Linear;

/// <summary>An edit to an existing issue: only the fields that are set are sent, and the labels are replaced as a whole set. Every reference is an id the caller already resolved.</summary>
/// <param name="Title">The new title, 1 to 255 characters.</param>
/// <param name="Description">The new description (up to 65,536 characters), or null to clear it.</param>
/// <param name="StateId">The workflow state's Linear id.</param>
/// <param name="AssigneeId">The assignee's Linear user id, or null to unassign.</param>
/// <param name="Priority">0 (none) to 4 (low).</param>
/// <param name="Estimate">The estimate, or null to clear it.</param>
/// <param name="CycleId">The cycle's Linear id, or null to take it out of its cycle.</param>
/// <param name="ProjectId">The project's Linear id, or null to take it out of its project.</param>
/// <param name="ParentId">The parent issue's Linear id, or null to detach it.</param>
/// <param name="LabelIds">The issue's whole label set.</param>
public sealed record LinearIssueChange(
    LinearFieldChange<string> Title = default,
    LinearFieldChange<string?> Description = default,
    LinearFieldChange<ExternalId> StateId = default,
    LinearFieldChange<ExternalId?> AssigneeId = default,
    LinearFieldChange<int> Priority = default,
    LinearFieldChange<double?> Estimate = default,
    LinearFieldChange<ExternalId?> CycleId = default,
    LinearFieldChange<ExternalId?> ProjectId = default,
    LinearFieldChange<ExternalId?> ParentId = default,
    LinearFieldChange<IReadOnlyList<ExternalId>> LabelIds = default)
{
    /// <summary>True when no field is set: there is nothing to send.</summary>
    public bool IsEmpty =>
        !(Title.IsSet || Description.IsSet || StateId.IsSet || AssigneeId.IsSet || Priority.IsSet || Estimate.IsSet || CycleId.IsSet || ProjectId.IsSet || ParentId.IsSet || LabelIds.IsSet);
}
