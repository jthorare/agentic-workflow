using Aw.Context.Core.Linear;
using Aw.Context.Core.Sources;

namespace Aw.Context.Infrastructure.Linear;

/// <summary>Reads the issue a mutation answered with into the view the facade stores.</summary>
internal static class LinearIssueViews
{
    /// <summary>The view, or null when a field the view needs is missing or isn't of the shape (the operation asks for all of them).</summary>
    /// <param name="issue">The issue as the mutation's reply has it.</param>
    /// <returns>The view or null.</returns>
    public static LinearIssueView? Of(LinearIssue issue)
    {
        var key = LinearIssueKey.Parse(issue.Identifier);
        if (issue.State is not { Id: { } stateText } state || issue.Team is not { } team || key.IsError
            || !ExternalId.TryFrom(issue.Id, out var id) || !ExternalId.TryFrom(stateText, out var stateId))
        {
            return null;
        }

        if (Labels(issue.Labels) is not { } labels)
        {
            return null;
        }

        return new LinearIssueView(
            id, key.Value, team.Key, issue.Title, issue.Description, stateId, state.Name, Optional(issue.Assignee), issue.Priority ?? 0, issue.Estimate,
            Optional(issue.Cycle), Optional(issue.Project), Optional(issue.Parent?.Id), labels, issue.UpdatedAt);
    }

    private static List<ExternalId>? Labels(LinearConnection<LinearNamed>? connection)
    {
        List<ExternalId> labels = [];
        foreach (var label in connection?.Nodes ?? [])
        {
            if (!ExternalId.TryFrom(label.Id, out var id))
            {
                return null;
            }

            labels.Add(id);
        }

        return labels;
    }

    private static ExternalId? Optional(LinearNamed? named) => Optional(named?.Id);

    private static ExternalId? Optional(string? id) => id is not null && ExternalId.TryFrom(id, out var parsed) ? parsed : null;
}
