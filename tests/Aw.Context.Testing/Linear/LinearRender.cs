using System.Text.Json.Nodes;

namespace Aw.Context.Testing.Linear;

/// <summary>Turns the state into the JSON nodes the queries select from. Every field the feed's operations ask for is here, so a query that asks for another one is a 400 and not a silent null.</summary>
internal static class LinearRender
{
    /// <summary>An issue. A comment or attachment carries its issue without the comments and attachments (<paramref name="withChildren"/> false), so rendering one doesn't render the other.</summary>
    public static JsonObject Issue(LinearStore s, FakeLinearIssue i, bool withChildren = true)
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
            ["project"] = project is null ? null : WithStatus(Ref(project)),
            ["cycle"] = cycle is null ? null : new JsonObject { ["id"] = cycle.Id, ["name"] = cycle.Name, ["number"] = s.Cycles.IndexOf(cycle) + 1 },
            ["parent"] = null,
            ["dueDate"] = null,
            ["sortOrder"] = -1000.0 * (s.Issues.IndexOf(i) + 1),
            ["labels"] = Nodes(i.Labels.Select(l => (JsonNode)Ref(LinearStore.FindRow(s.Labels, l)!))),
            ["comments"] = Nodes(withChildren ? s.Comments.Where(c => c.IssueId == i.Id).Select(c => Comment(s, c)) : []),
            ["attachments"] = Nodes(withChildren ? s.Attachments.Where(a => a.IssueId == i.Id).Select(a => Attachment(s, a)) : []),
            ["relations"] = Nodes([]),
            ["inverseRelations"] = Nodes([]),
        };
    }

    // why: the feed's first read filters on 'project: { status: { type } }', which the fake evaluates against the node, so an issue's project has one (every fake project is started)
    private static JsonObject WithStatus(JsonObject project)
    {
        project["status"] = new JsonObject { ["name"] = "Started", ["type"] = "started" };
        return project;
    }

    /// <summary>A label with its team (null for a workspace label), as 'issueLabels' filters it by team.</summary>
    public static JsonObject Label(LinearStore s, LinearRow l)
    {
        var node = Ref(l);
        var team = l.TeamId is null ? null : s.Teams.Find(t => t.Id == l.TeamId);
        node["team"] = team is null ? null : Ref(team, key: true);
        return node;
    }

    // a label inside a team's list: its colour too
    private static JsonObject LabelRef(LinearRow l)
    {
        var node = Ref(l);
        node["color"] = l.Color ?? "#bec2c8";
        return node;
    }

    /// <summary>The workspace, as 'organization' answers.</summary>
    public static JsonObject Organization(LinearStore s) => new() { ["id"] = "00000000-0000-4000-8000-0000000000f0", ["urlKey"] = s.UrlKey };

    public static JsonObject Comment(LinearStore s, FakeLinearComment c) => new()
    {
        ["id"] = c.Id,
        ["body"] = c.Body,
        ["createdAt"] = c.CreatedAt,
        ["updatedAt"] = c.UpdatedAt,
        ["user"] = Ref(LinearStore.FindRow(s.Members, c.User) ?? s.Viewer),
        ["issue"] = Commented(s, c.IssueId),
    };

    // why: Linear filters comments by 'issue: { team: { key } }', and a write answers with the comment's issue as it is after the write, so a comment carries its whole issue
    private static JsonObject Commented(LinearStore s, string issueId) =>
        s.Issues.Find(i => i.Id == issueId) is { } issue ? Issue(s, issue, withChildren: false) : new JsonObject { ["id"] = issueId, ["identifier"] = null, ["team"] = null };

    public static JsonObject Attachment(LinearStore s, FakeLinearAttachment a) => new()
    {
        ["id"] = a.Id,
        ["title"] = a.Title,
        ["url"] = a.Url,
        ["sourceType"] = null,
        ["createdAt"] = a.CreatedAt,
        ["issue"] = s.Issues.Find(i => i.Id == a.IssueId) is { } issue ? Issue(s, issue, withChildren: false) : new JsonObject { ["id"] = a.IssueId },
    };

    public static JsonObject Team(LinearRow t) => Ref(t, key: true);

    /// <summary>A team with the lists the pickers read: its states, the cycles it has, every member and its labels (workspace labels too).</summary>
    public static JsonObject TeamWithLookups(LinearStore s, LinearRow t)
    {
        var node = Ref(t, key: true);
        node["states"] = Nodes(s.States.Where(x => x.TeamId == t.Id).Select((x, position) => (JsonNode)new JsonObject { ["id"] = x.Id, ["name"] = x.Name, ["type"] = x.Type, ["position"] = position }));
        node["cycles"] = Nodes(s.Cycles.Where(c => c.TeamId is null || c.TeamId == t.Id).Select(c => (JsonNode)Cycle(s, c)));
        node["members"] = Nodes(s.Members.Select(m => (JsonNode)Member(m)));
        node["labels"] = Nodes(s.Labels.Where(l => l.TeamId is null || l.TeamId == t.Id).Select(l => (JsonNode)LabelRef(l)));
        return node;
    }

    public static JsonObject Cycle(LinearStore s, LinearRow c) =>
        new() { ["id"] = c.Id, ["name"] = c.Name, ["number"] = s.Cycles.IndexOf(c) + 1, ["startsAt"] = c.StartsAt, ["endsAt"] = c.EndsAt };

    public static JsonObject State(LinearStore s, LinearRow x) =>
        new() { ["id"] = x.Id, ["name"] = x.Name, ["type"] = x.Type, ["team"] = Ref(s.Teams.First(t => t.Id == x.TeamId), key: true) };

    public static JsonObject Member(LinearRow m) =>
        new() { ["id"] = m.Id, ["name"] = m.Name, ["displayName"] = m.Name, ["active"] = m.Active };

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
