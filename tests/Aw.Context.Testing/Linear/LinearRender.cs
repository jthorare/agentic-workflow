using System.Text.Json.Nodes;

namespace Aw.Context.Testing.Linear;

/// <summary>Turns the state into the JSON nodes the queries select from. Every field the feed's operations ask for is here, so a query that asks for another one is a 400 and not a silent null.</summary>
internal static class LinearRender
{
    public static JsonObject Issue(LinearStore s, FakeLinearIssue i)
    {
        var team = s.Teams.First(t => t.Key == i.Team);
        var state = s.States.First(x => x.TeamId == team.Id && x.Name == i.State);
        var assignee = LinearStore.FindRow(s.Members, i.Assignee);
        var project = LinearStore.FindRow(s.Projects, i.Project);
        var cycle = LinearStore.FindRow(s.Cycles, i.Cycle);
        return new JsonObject
        {
            ["id"] = i.Id,
            ["identifier"] = i.Identifier,
            ["title"] = i.Title,
            ["description"] = i.Description,
            ["url"] = $"https://linear.fake/{i.Team.ToUpperInvariant()}/issue/{i.Identifier}",
            ["createdAt"] = i.CreatedAt,
            ["updatedAt"] = i.UpdatedAt,
            ["archivedAt"] = i.ArchivedAt,
            ["completedAt"] = state.Type == "completed" ? i.UpdatedAt : null,
            ["canceledAt"] = state.Type == "canceled" ? i.UpdatedAt : null,
            ["priority"] = i.Priority,
            ["estimate"] = i.Estimate,
            ["state"] = new JsonObject { ["id"] = state.Id, ["name"] = state.Name, ["type"] = state.Type },
            ["team"] = Ref(team, key: true),
            ["creator"] = Ref(s.Viewer),
            ["assignee"] = assignee is null ? null : Ref(assignee),
            ["project"] = project is null ? null : Ref(project),
            ["cycle"] = cycle is null ? null : new JsonObject { ["id"] = cycle.Id, ["name"] = cycle.Name, ["number"] = s.Cycles.IndexOf(cycle) + 1 },
            ["parent"] = null,
            ["labels"] = Nodes(i.Labels.Select(l => (JsonNode)Ref(LinearStore.FindRow(s.Labels, l)!))),
            ["comments"] = Nodes(s.Comments.Where(c => c.IssueId == i.Id).Select(c => Comment(s, c))),
            ["attachments"] = Nodes(s.Attachments.Where(a => a.IssueId == i.Id).Select(Attachment)),
            ["relations"] = Nodes([]),
            ["inverseRelations"] = Nodes([]),
        };
    }

    /// <summary>A label with its team (null for a workspace label), as 'issueLabels' filters it by team.</summary>
    public static JsonObject Label(LinearStore s, LinearRow l)
    {
        var node = Ref(l);
        var team = l.TeamId is null ? null : s.Teams.Find(t => t.Id == l.TeamId);
        node["team"] = team is null ? null : Ref(team, key: true);
        return node;
    }

    public static JsonObject Comment(LinearStore s, FakeLinearComment c) => new()
    {
        ["id"] = c.Id,
        ["body"] = c.Body,
        ["createdAt"] = c.CreatedAt,
        ["updatedAt"] = c.UpdatedAt,
        ["user"] = Ref(LinearStore.FindRow(s.Members, c.User) ?? s.Viewer),
        ["issue"] = Commented(s, c.IssueId),
    };

    // why: Linear filters comments by 'issue: { team: { key: ... } }', so the issue carries its team
    private static JsonObject Commented(LinearStore s, string issueId)
    {
        var issue = s.Issues.Find(i => i.Id == issueId);
        var team = issue is null ? null : s.Teams.Find(t => t.Key == issue.Team);
        return new JsonObject { ["id"] = issueId, ["identifier"] = issue?.Identifier, ["team"] = team is null ? null : Ref(team, key: true) };
    }

    public static JsonObject Attachment(FakeLinearAttachment a) => new()
    {
        ["id"] = a.Id,
        ["title"] = a.Title,
        ["url"] = a.Url,
        ["sourceType"] = null,
        ["createdAt"] = a.CreatedAt,
        ["issue"] = new JsonObject { ["id"] = a.IssueId },
    };

    public static JsonObject Team(LinearRow t) => Ref(t, key: true);

    public static JsonObject State(LinearStore s, LinearRow x) =>
        new() { ["id"] = x.Id, ["name"] = x.Name, ["type"] = x.Type, ["team"] = Ref(s.Teams.First(t => t.Id == x.TeamId), key: true) };

    public static JsonObject Member(LinearRow m) =>
        new() { ["id"] = m.Id, ["name"] = m.Name, ["displayName"] = m.Name, ["active"] = true };

    public static JsonObject Project(LinearStore s, LinearRow p) => new()
    {
        ["id"] = p.Id,
        ["name"] = p.Name,
        ["slugId"] = p.Id[..8],
        ["description"] = null,
        ["url"] = "https://linear.fake/project/" + p.Id[..8],
        ["state"] = "started",
        ["createdAt"] = null,
        ["updatedAt"] = null,
        ["startDate"] = null,
        ["targetDate"] = null,
        ["startedAt"] = null,
        ["completedAt"] = null,
        ["canceledAt"] = null,
        ["status"] = new JsonObject { ["name"] = "In Progress", ["type"] = "started" },
        ["lead"] = null,
        ["teams"] = Nodes(s.Teams.Select(t => (JsonNode)Ref(t, key: true))),
        ["lastUpdate"] = null,
    };

    public static JsonObject Ref(LinearRow row, bool key = false)
    {
        var node = new JsonObject { ["id"] = row.Id, ["name"] = row.Name };
        if (key)
        {
            node["key"] = row.Key;
        }

        return node;
    }

    public static JsonObject Nodes(IEnumerable<JsonNode> nodes) => new() { ["nodes"] = new JsonArray([.. nodes]) };
}
