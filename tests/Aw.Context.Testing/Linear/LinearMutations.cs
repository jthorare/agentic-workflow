using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Context.Testing.Fakes;

namespace Aw.Context.Testing.Linear;

/// <summary>
/// Runs the mutations against the state. Each returns '{ success, ... }' for the query to cut down. A repeated client id is
/// refused by <see cref="LinearErrors.DuplicateId"/>; which mutations move updatedAt is <see cref="LinearUpdatedAt"/>.
/// </summary>
internal sealed class LinearMutations(LinearStore store, FakeLinearSwitches switches)
{
    public JsonNode Run(GraphQlField field) => field.Name switch
    {
        "issueCreate" => IssueCreate(field),
        "issueUpdate" => IssueUpdate(field),
        "commentCreate" => CommentCreate(field),
        "commentUpdate" => CommentUpdate(field),
        "attachmentLinkURL" => AttachmentLink(field),
        "issueArchive" => IssueArchive(field),
        "issueLabelCreate" => LabelCreate(field),
        _ => throw FakeRequestException.Invalid($"Cannot query field \"{field.Name}\" on type \"Mutation\". The fake Linear answers only: {string.Join(", ", LinearSchema.Mutations)}."),
    };

    private static JsonObject Payload(string name, JsonNode? entity) =>
        new() { ["success"] = true, ["lastSyncId"] = 1, [name] = entity };

    private static string? Str(JsonObject input, string key) =>
        input.TryGetPropertyValue(key, out var v) && v is not null
            ? v is JsonValue s && s.GetValueKind() == JsonValueKind.String ? s.GetValue<string>() : throw FakeRequestException.Invalid($"Field \"{key}\" takes a String.")
            : null;

    private static string Required(JsonObject input, string key) =>
        Str(input, key) ?? throw FakeRequestException.Invalid($"Field \"{key}\" of the input is required.");

    private static int? Int(JsonObject input, string key) =>
        input.TryGetPropertyValue(key, out var v) && v is not null
            ? v is JsonValue n && n.TryGetValue<int>(out var number) ? number : throw FakeRequestException.Invalid($"Field \"{key}\" takes an Int.")
            : null;

    private static JsonObject Input(GraphQlField field, string type, string argument = "input")
    {
        var input = field.Arguments.GetValueOrDefault(argument) as JsonObject
            ?? throw FakeRequestException.Invalid($"Field \"{field.Name}\" needs an object '{argument}' argument ({type}).");
        var known = LinearSchema.Inputs[type];
        var unknown = input.Select(p => p.Key).FirstOrDefault(k => known.All(f => f.Name != k));
        if (unknown is not null)
        {
            throw FakeRequestException.Invalid($"Field \"{unknown}\" is not defined by type \"{type}\" (fields: {string.Join(", ", known.Select(f => f.Name))}).");
        }

        var missing = known.FirstOrDefault(f => f.Required && !input.ContainsKey(f.Name) && type.EndsWith("CreateInput", StringComparison.Ordinal));
        if (missing is not null)
        {
            throw FakeRequestException.Invalid($"Field \"{type}.{missing.Name}\" of required type \"{missing.Type}!\" was not provided.");
        }

        return input;
    }

    private static string Id(GraphQlField field) =>
        LinearResolver.Text(field, "id") ?? throw FakeRequestException.Invalid($"Field \"{field.Name}\" needs a string 'id' argument.");

    private string? ClientId(JsonObject input, string kind, Func<string, bool> exists)
    {
        var id = Str(input, "id");
        return id is not null && (switches.RepeatClientId || exists(id)) ? throw LinearErrors.DuplicateId(kind, id) : id;
    }

    private FakeLinearIssue Bump(FakeLinearIssue issue, string mutation) =>
        LinearUpdatedAt.Bumps(mutation) ? issue with { UpdatedAt = store.Now() } : issue;

    private FakeLinearIssue Issue(string idOrIdentifier) =>
        store.FindIssue(idOrIdentifier) ?? throw LinearErrors.NotFound("Issue", idOrIdentifier);

    private JsonObject IssueCreate(GraphQlField field)
    {
        var input = Input(field, "IssueCreateInput");
        var clientId = ClientId(input, "Issue", id => store.Issues.Exists(i => i.Id == id));
        var teamId = Required(input, "teamId");
        var team = store.Teams.Find(t => t.Id == teamId) ?? throw LinearErrors.NotFound("Team", teamId);
        var now = store.Now();
        var backlog = store.States.Find(s => s.TeamId == team.Id && s.Type == "backlog") ?? store.States.First(s => s.TeamId == team.Id);
        var blank = new FakeLinearIssue(
            clientId ?? store.NewId(), store.NextIdentifier(team.Key!), Required(input, "title"), Description: null, backlog.Name, Assignee: null, Priority: 0,
            Estimate: null, Cycle: null, Project: null, Labels: [], team.Key!, now, now, ArchivedAt: null);
        var issue = Apply(blank, team, input);
        store.Issues.Add(issue);
        store.LastWrittenIssue = issue.Id;
        return Payload("issue", LinearRender.Issue(store, issue));
    }

    private JsonObject IssueUpdate(GraphQlField field)
    {
        var issue = Issue(Id(field));
        var input = Input(field, "IssueUpdateInput");
        var team = store.Teams.First(t => t.Key == issue.Team);
        if (Str(input, "teamId") is { } toId && toId != team.Id)
        {
            var target = store.Teams.Find(t => t.Id == toId) ?? throw LinearErrors.NotFound("Team", toId);
            issue = MoveTo(issue, target);
            team = target;
        }

        issue = Bump(Apply(issue, team, input), "issueUpdate");
        store.Save(issue);
        return Payload("issue", LinearRender.Issue(store, issue));
    }

    private FakeLinearIssue Apply(FakeLinearIssue issue, LinearRow team, JsonObject input)
    {
        if (input.ContainsKey("title"))
        {
            issue = issue with { Title = Required(input, "title") };
        }

        if (input.ContainsKey("description"))
        {
            issue = issue with { Description = Str(input, "description") };
        }

        if (input.ContainsKey("priority"))
        {
            var priority = Int(input, "priority") ?? 0;
            issue = priority is >= 0 and <= 4 ? issue with { Priority = priority } : throw FakeRequestException.Invalid("Field \"priority\" must be 0 to 4.");
        }

        if (input.ContainsKey("estimate"))
        {
            issue = issue with { Estimate = Int(input, "estimate") };
        }

        return ApplyRefs(issue, team, input);
    }

    private FakeLinearIssue ApplyRefs(FakeLinearIssue issue, LinearRow team, JsonObject input)
    {
        if (Str(input, "stateId") is { } stateId)
        {
            var state = store.States.Find(s => s.Id == stateId) ?? throw LinearErrors.NotFound("WorkflowState", stateId);
            issue = state.TeamId == team.Id ? issue with { State = state.Name } : throw FakeRequestException.Invalid($"The workflow state '{stateId}' belongs to another team.");
        }

        if (input.ContainsKey("assigneeId"))
        {
            issue = issue with { Assignee = NameOf(store.Members, Str(input, "assigneeId"), "User") };
        }

        if (input.ContainsKey("cycleId"))
        {
            issue = issue with { Cycle = NameOf(store.Cycles, Str(input, "cycleId"), "Cycle") };
        }

        if (input.ContainsKey("projectId"))
        {
            issue = issue with { Project = NameOf(store.Projects, Str(input, "projectId"), "Project") };
        }

        if (input.ContainsKey("labelIds"))
        {
            var ids = input["labelIds"] as JsonArray ?? [];
            issue = issue with { Labels = [.. ids.Select(n => NameOf(store.Labels, (string?)n, "IssueLabel")!)] };
        }

        return issue;
    }

    private static string? NameOf(List<LinearRow> rows, string? id, string kind) =>
        id is null ? null : rows.Find(r => r.Id == id)?.Name ?? throw LinearErrors.NotFound(kind, id);

    // why: same id, a fresh identifier in the target team; the old identifier keeps resolving, as in Linear (Ph0 checks)
    private FakeLinearIssue MoveTo(FakeLinearIssue issue, LinearRow target)
    {
        var state = store.States.Find(s => s.TeamId == target.Id && s.Name == issue.State) ?? store.States.First(s => s.TeamId == target.Id);
        store.Aliases[issue.Identifier] = issue.Id;
        return issue with { Identifier = store.NextIdentifier(target.Key!), Team = target.Key!, State = state.Name };
    }

    public FakeLinearIssue? MoveLastWritten(string teamKey)
    {
        var issue = store.LastWrittenIssue is { } id ? store.Issues.Find(i => i.Id == id) : null;
        var target = store.Teams.Find(t => t.Key == teamKey);
        if (issue is null || target is null || issue.Team == teamKey)
        {
            return null;
        }

        var moved = issue with { UpdatedAt = store.Now() };
        moved = MoveTo(moved, target);
        store.Save(moved);
        return moved;
    }

    public FakeLinearIssue? TeammateEditsLastWritten()
    {
        var issue = store.LastWrittenIssue is { } id ? store.Issues.Find(i => i.Id == id) : null;
        if (issue is null)
        {
            return null;
        }

        var edited = issue with { Estimate = (issue.Estimate ?? 0) + 1, UpdatedAt = store.Now() };
        store.Save(edited);
        return edited;
    }

    private JsonObject CommentCreate(GraphQlField field)
    {
        var input = Input(field, "CommentCreateInput");
        var clientId = ClientId(input, "Comment", id => store.Comments.Exists(c => c.Id == id));
        var issue = Issue(Required(input, "issueId"));
        var now = store.Now();
        var comment = new FakeLinearComment(clientId ?? store.NewId(), issue.Id, Required(input, "body"), store.Viewer.Name, now, now);
        store.Comments.Add(comment);
        store.Save(Bump(issue, field.Name));
        return Payload("comment", LinearRender.Comment(store, comment));
    }

    private JsonObject CommentUpdate(GraphQlField field)
    {
        var id = Id(field);
        var input = Input(field, "CommentUpdateInput");
        var at = store.Comments.FindIndex(c => c.Id == id);
        if (at < 0)
        {
            throw LinearErrors.NotFound("Comment", id);
        }

        var comment = store.Comments[at] with { Body = Required(input, "body"), UpdatedAt = store.Now() };
        store.Comments[at] = comment;
        store.Save(Bump(Issue(comment.IssueId), field.Name));
        return Payload("comment", LinearRender.Comment(store, comment));
    }

    // why: a link to a URL already on the issue updates that attachment instead of adding a second one (Ph0 checks)
    private JsonObject AttachmentLink(GraphQlField field)
    {
        var issue = Issue(LinearResolver.Text(field, "issueId") ?? throw FakeRequestException.Invalid("Field \"attachmentLinkURL\" needs a string 'issueId' argument."));
        var url = LinearResolver.Text(field, "url") ?? throw FakeRequestException.Invalid("Field \"attachmentLinkURL\" needs a string 'url' argument.");
        var title = LinearResolver.Text(field, "title") ?? url;
        var at = store.Attachments.FindIndex(a => a.IssueId == issue.Id && a.Url == url);
        var attachment = at >= 0
            ? store.Attachments[at] with { Title = title }
            : new FakeLinearAttachment(store.NewId(), issue.Id, url, title, store.Now());
        if (at >= 0)
        {
            store.Attachments[at] = attachment;
        }
        else
        {
            store.Attachments.Add(attachment);
        }

        store.Save(Bump(issue, field.Name));
        return Payload("attachment", LinearRender.Attachment(attachment));
    }

    private JsonObject IssueArchive(GraphQlField field)
    {
        var issue = Issue(Id(field));
        issue = Bump(issue with { ArchivedAt = store.Now() }, field.Name);
        store.Save(issue);
        return Payload("entity", new JsonObject { ["id"] = issue.Id, ["identifier"] = issue.Identifier });
    }

    private JsonObject LabelCreate(GraphQlField field)
    {
        var input = Input(field, "IssueLabelCreateInput");
        var clientId = ClientId(input, "IssueLabel", id => store.Labels.Exists(l => l.Id == id));
        var label = new LinearRow(clientId ?? store.NewId(), Required(input, "name"));
        store.Labels.Add(label);
        return Payload("issueLabel", LinearRender.Ref(label));
    }
}
