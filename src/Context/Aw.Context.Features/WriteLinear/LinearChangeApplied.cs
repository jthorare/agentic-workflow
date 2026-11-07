using Aw.Context.Core.Linear;

namespace Aw.Context.Features.WriteLinear;

/// <summary>Whether an issue as Linear has it now holds a change: every field the change sent is at the value it sent (the one re-read after a write got no answer, Linear hub spec 2026-11-04 §5.5).</summary>
internal static class LinearChangeApplied
{
    /// <summary>Compares the sent fields with the issue.</summary>
    /// <param name="change">The change that was sent.</param>
    /// <param name="issue">The issue as the re-read saw it.</param>
    /// <returns>True when every field the change set holds the value it set.</returns>
    public static bool In(LinearIssueChange change, LinearIssueView issue)
    {
        ArgumentNullException.ThrowIfNull(change);
        ArgumentNullException.ThrowIfNull(issue);
        return Holds(change.Title, issue.Title)
            && Text(change.Description, issue.Description)
            && Holds(change.StateId, issue.StateId)
            && Holds(change.AssigneeId, issue.AssigneeId)
            && Holds(change.Priority, issue.Priority)
            && Holds(change.Estimate, issue.Estimate)
            && Holds(change.CycleId, issue.CycleId)
            && Holds(change.ProjectId, issue.ProjectId)
            && Holds(change.ParentId, issue.ParentId)
            && Labels(change.LabelIds, issue.LabelIds);
    }

    private static bool Holds<T>(LinearFieldChange<T> field, T current) => !field.IsSet || EqualityComparer<T>.Default.Equals(field.Value, current);

    // why: Linear stores a description cleared with "" as none
    private static bool Text(LinearFieldChange<string?> field, string? current) => !field.IsSet || string.Equals(field.Value ?? "", current ?? "", StringComparison.Ordinal);

    private static bool Labels(LinearFieldChange<IReadOnlyList<Core.Sources.ExternalId>> field, IReadOnlyList<Core.Sources.ExternalId> current) =>
        !field.IsSet || (field.Value ?? []).ToHashSet().SetEquals(current);
}
