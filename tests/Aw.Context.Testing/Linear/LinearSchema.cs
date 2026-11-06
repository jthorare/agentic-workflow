using System.Text.Json.Nodes;

namespace Aw.Context.Testing.Linear;

/// <summary>The part of Linear's schema the fake knows: the mutation input types (one table, used to refuse unknown fields and to answer introspection) and the root field names.</summary>
internal static class LinearSchema
{
    public static readonly IReadOnlyList<string> Queries =
        ["issues", "issue", "viewer", "teams", "users", "issueLabels", "workflowStates", "cycles", "projects", "documents", "projectUpdates"];

    public static readonly IReadOnlyList<string> Mutations =
        ["issueCreate", "issueUpdate", "commentCreate", "commentUpdate", "attachmentLinkURL", "issueArchive", "issueLabelCreate"];

    public static readonly IReadOnlyDictionary<string, IReadOnlyList<LinearInputField>> Inputs = new Dictionary<string, IReadOnlyList<LinearInputField>>(StringComparer.Ordinal)
    {
        ["IssueCreateInput"] =
        [
            new("id", "String"), new("title", "String", Required: true), new("description", "String"), new("teamId", "String", Required: true),
            new("stateId", "String"), new("assigneeId", "String"), new("priority", "Int"), new("estimate", "Int"),
            new("cycleId", "String"), new("projectId", "String"), new("labelIds", "String", List: true),
        ],
        ["IssueUpdateInput"] =
        [
            new("title", "String"), new("description", "String"), new("teamId", "String"), new("stateId", "String"),
            new("assigneeId", "String"), new("priority", "Int"), new("estimate", "Int"), new("cycleId", "String"),
            new("projectId", "String"), new("labelIds", "String", List: true),
        ],
        ["CommentCreateInput"] = [new("id", "String"), new("body", "String", Required: true), new("issueId", "String", Required: true)],
        ["CommentUpdateInput"] = [new("body", "String", Required: true)],
        ["IssueLabelCreateInput"] = [new("id", "String"), new("name", "String", Required: true), new("teamId", "String"), new("color", "String")],
    };

    /// <summary>The answer to __type(name:), or null for a type the fake doesn't know.</summary>
    public static JsonObject? Type(string name) =>
        Inputs.TryGetValue(name, out var fields)
            ? new JsonObject
            {
                ["name"] = name,
                ["kind"] = "INPUT_OBJECT",
                ["inputFields"] = new JsonArray([.. fields.Select(Field)]),
            }
            : null;

    /// <summary>The answer to __schema.</summary>
    public static JsonObject Schema() => new()
    {
        ["queryType"] = Root("Query", Queries),
        ["mutationType"] = Root("Mutation", Mutations),
    };

    private static JsonObject Root(string name, IReadOnlyList<string> fields) =>
        new() { ["name"] = name, ["fields"] = new JsonArray([.. fields.Select(f => (JsonNode)new JsonObject { ["name"] = f })]) };

    private static JsonObject Field(LinearInputField field)
    {
        var scalar = Wrap("SCALAR", field.Type, ofType: null);
        var type = field.List ? Wrap("LIST", name: null, Wrap("NON_NULL", name: null, scalar)) : field.Required ? Wrap("NON_NULL", name: null, scalar) : scalar;
        return new JsonObject { ["name"] = field.Name, ["description"] = null, ["defaultValue"] = null, ["type"] = type };
    }

    private static JsonObject Wrap(string kind, string? name, JsonObject? ofType) =>
        new() { ["name"] = name, ["kind"] = kind, ["ofType"] = ofType };
}
