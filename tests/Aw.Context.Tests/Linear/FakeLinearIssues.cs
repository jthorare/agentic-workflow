using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aw.Context.Tests.Linear;

/// <summary>
/// A fake Linear that answers the 'issues' root field from a list: updatedAt gt or gte, newest first, a 'first' and an 'after' cursor (the offset of the last node served). It serves the nodes it is given as written, so a test controls state, parent and relations.
/// </summary>
internal sealed class FakeLinearIssues(IReadOnlyList<JsonObject> nodes) : HttpMessageHandler
{
    private static readonly JsonSerializerOptions _json = new();

    /// <summary>The 'variables' of each request.</summary>
    public List<JsonObject> Variables { get; } = [];

    /// <summary>One issue node as Linear serves it.</summary>
    /// <param name="id">The id.</param>
    /// <param name="updatedAt">The updatedAt.</param>
    /// <returns>The node.</returns>
    public static JsonObject Node(string id, DateTimeOffset updatedAt) => new()
    {
        ["id"] = id,
        ["identifier"] = "SPL-" + id,
        ["title"] = "Title " + id,
        ["description"] = "Text " + id,
        ["url"] = "https://linear.app/x/issue/SPL-" + id,
        ["createdAt"] = updatedAt.AddDays(-2).UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
        ["updatedAt"] = updatedAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
        ["state"] = new JsonObject { ["name"] = "Todo", ["type"] = "unstarted" },
        ["team"] = new JsonObject { ["id"] = "team-1", ["key"] = "SPL", ["name"] = "Split" },
    };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken).ConfigureAwait(false))!.AsObject();
        var variables = body["variables"]?.AsObject() ?? [];
        Variables.Add((JsonObject)variables.DeepClone());
        var rows = Matching(variables["filter"]?.AsObject());
        var skip = variables["after"] is { } after ? int.Parse(after.GetValue<string>(), CultureInfo.InvariantCulture) : 0;
        var reply = Reply(rows, skip, variables["first"]?.GetValue<int>() ?? 50);
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(reply.ToJsonString(_json), Encoding.UTF8, "application/json") };
    }

    private List<JsonObject> Matching(JsonObject? query) => query?["or"] is JsonArray any ? OrMatching(any) : InRange(query?["updatedAt"]?.AsObject());

    private List<JsonObject> InRange(JsonObject? filter)
    {
        var gt = Bound(filter, "gt") ?? DateTimeOffset.MinValue;
        var gte = Bound(filter, "gte") ?? DateTimeOffset.MinValue;
        return [.. nodes.Where(n => Updated(n) > gt && Updated(n) >= gte).OrderByDescending(Updated).ThenBy(n => n["id"]!.GetValue<string>(), StringComparer.Ordinal)];
    }

    // why: the first-read filter is "updated in the window, or open in an open project"; the fake reads the first branch like a plain updatedAt filter and the second from the node's state type and project status type
    private List<JsonObject> OrMatching(JsonArray branches)
    {
        var window = branches.OfType<JsonObject>().FirstOrDefault(static b => b["updatedAt"] is not null)?["updatedAt"]?.AsObject();
        var open = branches.OfType<JsonObject>().FirstOrDefault(static b => b["state"] is not null);
        var inWindow = InRange(window);
        var rest = open is null ? [] : nodes.Where(n => OpenInOpenProject(n) && !inWindow.Contains(n));
        return [.. inWindow.Concat(rest).OrderByDescending(Updated).ThenBy(n => n["id"]!.GetValue<string>(), StringComparer.Ordinal)];
    }

    private static bool OpenInOpenProject(JsonObject node) =>
        node["project"] is JsonObject project && !Closed(node["state"]?["type"]) && !Closed(project["status"]?["type"]);

    private static bool Closed(JsonNode? type) => type?.GetValue<string>() is { } t && (string.Equals(t, "completed", StringComparison.Ordinal) || string.Equals(t, "canceled", StringComparison.Ordinal));

    private static JsonObject Reply(List<JsonObject> rows, int skip, int first)
    {
        var page = rows.Skip(skip).Take(first).ToList();
        var end = skip + page.Count;
        return new JsonObject
        {
            ["data"] = new JsonObject
            {
                ["issues"] = new JsonObject
                {
                    ["nodes"] = new JsonArray([.. page.Select(n => n.DeepClone())]),
                    ["pageInfo"] = new JsonObject { ["hasNextPage"] = end < rows.Count, ["endCursor"] = end.ToString(CultureInfo.InvariantCulture) },
                },
            },
        };
    }

    private static DateTimeOffset Updated(JsonObject node) => DateTimeOffset.Parse(node["updatedAt"]!.GetValue<string>(), CultureInfo.InvariantCulture);

    private static DateTimeOffset? Bound(JsonObject? filter, string op) => filter?[op] is { } v ? DateTimeOffset.Parse(v.GetValue<string>(), CultureInfo.InvariantCulture) : null;
}
