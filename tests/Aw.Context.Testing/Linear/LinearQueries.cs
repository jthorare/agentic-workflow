using System.Text.Json.Nodes;
using Aw.Context.Testing.Fakes;

namespace Aw.Context.Testing.Linear;

/// <summary>Answers the root fields of a query from the state: the same fields as the replaying fake, plus teams, users, labels, states, cycles and the introspection a probe asks.</summary>
internal sealed class LinearQueries(LinearStore store)
{
    private static readonly HashSet<string> _connection = new(StringComparer.Ordinal) { "filter", "first", "after", "orderBy", "includeArchived" };

    public (JsonNode? Value, string? Error) Resolve(GraphQlField field) => field.Name switch
    {
        "__type" or "__schema" => (Introspect(field), null),
        "viewer" => (LinearShape.Select(LinearRender.Member(store.Viewer), field, field.Name), null),
        "organization" => (LinearShape.Select(LinearRender.Organization(store), field, field.Name), null),
        "issue" => Issue(field),
        "comment" => Comment(field),
        "issues" => (Select(field, Issues(field), newestFirst: true), null),
        "comments" => (Select(field, store.Comments.Select(c => (JsonNode)LinearRender.Comment(store, c)), newestFirst: true), null),
        _ => (Select(field, Rows(field.Name), newestFirst: false), null),
    };

    private static JsonNode? Introspect(GraphQlField field)
    {
        if (field.Name == "__schema")
        {
            return LinearShape.Select(LinearSchema.Schema(), field, field.Name);
        }

        LinearResolver.Known(field, new HashSet<string>(["name"], StringComparer.Ordinal));
        var type = LinearSchema.Type(LinearResolver.Text(field, "name") ?? "");
        return type is null ? null : LinearShape.Select(type, field, field.Name);
    }

    private IEnumerable<JsonNode> Rows(string name) => name switch
    {
        "teams" => store.Teams.Select(t => (JsonNode)LinearRender.TeamWithLookups(store, t)),
        "users" => store.Members.Select(m => (JsonNode)LinearRender.Member(m)),
        "issueLabels" => store.Labels.Select(l => (JsonNode)LinearRender.Label(store, l)),
        "workflowStates" => store.States.Select(x => (JsonNode)LinearRender.State(store, x)),
        "cycles" => store.Cycles.Select(c => (JsonNode)LinearRender.Cycle(store, c)),
        "projects" => store.Projects.Select(p => (JsonNode)LinearRender.Project(store, p)),
        "documents" or "projectUpdates" => [],
        _ => throw FakeRequestException.Invalid($"Cannot query field \"{name}\" on type \"Query\". The fake Linear answers only: {string.Join(", ", LinearSchema.Queries)}."),
    };

    private (JsonNode? Value, string? Error) Issue(GraphQlField field)
    {
        LinearResolver.Known(field, new HashSet<string>(["id"], StringComparer.Ordinal));
        var id = LinearResolver.Text(field, "id") ?? throw FakeRequestException.Invalid("Field \"issue\" needs a string 'id' argument.");
        var issue = store.FindIssue(id);
        return issue is null
            ? (null, $"Entity not found: Issue - Could not find referenced Issue '{id}'.")
            : (LinearShape.Select(LinearRender.Issue(store, issue), field, field.Name), null);
    }

    private (JsonNode? Value, string? Error) Comment(GraphQlField field)
    {
        LinearResolver.Known(field, new HashSet<string>(["id"], StringComparer.Ordinal));
        var id = LinearResolver.Text(field, "id") ?? throw FakeRequestException.Invalid("Field \"comment\" needs a string 'id' argument.");
        var comment = store.Comments.Find(c => c.Id == id);
        return comment is null
            ? (null, $"Entity not found: Comment - Could not find referenced Comment '{id}'.")
            : (LinearShape.Select(LinearRender.Comment(store, comment), field, field.Name), null);
    }

    private IEnumerable<JsonNode> Issues(GraphQlField field)
    {
        var archived = field.Arguments.GetValueOrDefault("includeArchived") is JsonValue v && v.TryGetValue<bool>(out var include) && include;
        return store.Issues.Where(i => archived || i.ArchivedAt is null).Select(i => (JsonNode)LinearRender.Issue(store, i));
    }

    private static JsonNode Select(GraphQlField field, IEnumerable<JsonNode> nodes, bool newestFirst)
    {
        LinearResolver.Known(field, _connection);
        var filter = field.Arguments.GetValueOrDefault("filter") switch
        {
            null => null,
            JsonObject o => o,
            _ => throw FakeRequestException.Invalid("'filter' takes an object."),
        };
        var order = LinearResolver.Text(field, "orderBy") ?? "createdAt";
        if (order is not ("createdAt" or "updatedAt"))
        {
            throw FakeRequestException.Invalid($"orderBy '{order}' isn't a PaginationOrderBy; use createdAt or updatedAt.");
        }

        var found = nodes.Where(n => filter is null || LinearFilter.Matches(n, filter)).ToList();
        var ordered = found;
        if (newestFirst)
        {
            ordered = [.. found.OrderByDescending(n => (string?)n[order], StringComparer.Ordinal).ThenBy(n => (string?)n["id"], StringComparer.Ordinal)];
        }

        return LinearShape.Select(LinearResolver.Page(field, ordered), field, field.Name) ?? new JsonObject();
    }
}
