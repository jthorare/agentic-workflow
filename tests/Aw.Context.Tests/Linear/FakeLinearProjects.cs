using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aw.Context.Tests.Linear;

/// <summary>A fake Linear that answers the 'projects' root field from a list: updatedAt gt, newest first, a 'first' and an 'after' cursor (the offset of the last node served). It records each request's variables.</summary>
internal sealed class FakeLinearProjects(IReadOnlyList<JsonObject> nodes) : HttpMessageHandler
{
    private static readonly JsonSerializerOptions _json = new();

    /// <summary>The 'variables' of each request.</summary>
    public List<JsonObject> Variables { get; } = [];

    /// <summary>One project node as Linear serves it: started, in team SPL, no lead, no update.</summary>
    /// <param name="id">The id.</param>
    /// <param name="updatedAt">The updatedAt.</param>
    /// <returns>The node.</returns>
    public static JsonObject Node(string id, DateTimeOffset updatedAt) => new()
    {
        ["id"] = id,
        ["name"] = "Project " + id,
        ["description"] = "About " + id,
        ["url"] = "https://linear.app/x/project/" + id,
        ["createdAt"] = updatedAt.AddDays(-30).UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
        ["updatedAt"] = updatedAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
        ["status"] = new JsonObject { ["name"] = "In Progress", ["type"] = "started" },
        ["teams"] = new JsonObject { ["nodes"] = new JsonArray(new JsonObject { ["key"] = "SPL" }) },
    };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken).ConfigureAwait(false))!.AsObject();
        var variables = body["variables"]?.AsObject() ?? [];
        Variables.Add((JsonObject)variables.DeepClone());
        var gt = variables["filter"]?["updatedAt"]?["gt"] is { } v ? DateTimeOffset.Parse(v.GetValue<string>(), CultureInfo.InvariantCulture) : DateTimeOffset.MinValue;
        var rows = nodes.Where(n => Updated(n) > gt).OrderByDescending(Updated).ThenBy(n => n["id"]!.GetValue<string>(), StringComparer.Ordinal).ToList();
        var skip = variables["after"] is { } after ? int.Parse(after.GetValue<string>(), CultureInfo.InvariantCulture) : 0;
        var page = rows.Skip(skip).Take(variables["first"]?.GetValue<int>() ?? 50).ToList();
        var end = skip + page.Count;
        var reply = new JsonObject
        {
            ["data"] = new JsonObject
            {
                ["projects"] = new JsonObject
                {
                    ["nodes"] = new JsonArray([.. page.Select(n => n.DeepClone())]),
                    ["pageInfo"] = new JsonObject { ["hasNextPage"] = end < rows.Count, ["endCursor"] = end.ToString(CultureInfo.InvariantCulture) },
                },
            },
        };
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(reply.ToJsonString(_json), Encoding.UTF8, "application/json") };
    }

    private static DateTimeOffset Updated(JsonObject node) => DateTimeOffset.Parse(node["updatedAt"]!.GetValue<string>(), CultureInfo.InvariantCulture);
}
