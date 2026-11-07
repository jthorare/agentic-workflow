using Aw.Context.Core;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Linear;

/// <summary>Reads an issue as the live operations select it into the view the Linear hub's writes work with.</summary>
internal static class LinearIssueViews
{
    /// <summary>Maps one issue.</summary>
    /// <param name="issue">The issue as served; it needs its state (id and name), team key and a well-formed identifier.</param>
    /// <returns>The view, or SND-CONTEXT-072 when Linear left out something the view can't do without.</returns>
    public static Result<LinearIssueView> Of(LinearIssue issue)
    {
        ArgumentNullException.ThrowIfNull(issue);
        var key = LinearIssueKey.Parse(issue.Identifier);
        if (key.IsError || issue.State is not { Id: { } stateId } state || issue.Team is not { } team)
        {
            return ContextErrors.LinearUnexpectedReply.With($"Linear answered issue {issue.Id} without a usable key, state or team; the live-read query may need updating.");
        }

        return View(issue, key.Value, team.Key, stateId, state.Name);
    }

    private static LinearIssueView View(LinearIssue issue, LinearIssueKey key, string teamKey, string stateId, string stateName) =>
        new(
            ExternalId.From(issue.Id),
            key,
            teamKey,
            issue.Title,
            issue.Description,
            ExternalId.From(stateId),
            stateName,
            Optional(issue.Assignee?.Id),
            issue.Priority ?? 0,
            issue.Estimate,
            Optional(issue.Cycle?.Id),
            Optional(issue.Project?.Id),
            Optional(issue.Parent?.Id),
            [.. (issue.Labels?.Nodes ?? []).Select(static l => ExternalId.From(l.Id))],
            issue.UpdatedAt);

    private static ExternalId? Optional(string? id) => id is null ? null : ExternalId.From(id);
}
