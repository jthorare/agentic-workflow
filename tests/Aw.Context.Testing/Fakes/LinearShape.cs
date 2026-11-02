using System.Text.Json.Nodes;

namespace Aw.Context.Testing.Fakes;

/// <summary>
/// Cuts a recorded value down to what the query selected, as a GraphQL server answers: only the selected fields,
/// under their aliases, recursing into objects and lists. A field the recordings don't have, an object without a
/// sub-selection, or a scalar with one is a 400, so a hand-written query that selects the wrong thing fails here and
/// not against Linear. A nested connection takes 'first' and 'orderBy' (updatedAt or createdAt, newest first, as Linear
/// orders a nested connection) and gets a computed 'pageInfo' (the recordings hold every child); other nested arguments are refused.
/// </summary>
internal static class LinearShape
{
    public static JsonNode? Select(JsonNode? value, GraphQlField field, string path)
    {
        if (field.Selections is null)
        {
            return value is JsonObject or JsonArray
                ? throw FakeRequestException.Invalid($"Field \"{field.Name}\" at {path} is an object; select its subfields.")
                : value?.DeepClone();
        }

        return value switch
        {
            null => null,
            JsonArray items => new JsonArray([.. items.Select(item => Select(item, field, path))]),
            JsonObject node => Object(node, field.Selections, path),
            _ => throw FakeRequestException.Invalid($"Field \"{field.Name}\" at {path} is a scalar; it takes no selection."),
        };
    }

    private static JsonObject Object(JsonObject node, IReadOnlyList<GraphQlField> selections, string path)
    {
        var shaped = new JsonObject();
        foreach (var child in selections)
        {
            var childPath = path + "." + child.Name;
            if (!node.TryGetPropertyValue(child.Name, out var value))
            {
                throw FakeRequestException.Invalid(
                    $"Cannot query field \"{child.Name}\" at {path}: the fixture's recordings have no such field (fields here: {string.Join(", ", node.Select(p => p.Key))}).");
            }

            shaped[child.ResponseKey] = Select(Nested(value, child, childPath), child, childPath);
        }

        return shaped;
    }

    private static JsonNode? Nested(JsonNode? value, GraphQlField child, string path)
    {
        var connection = value is JsonObject c && c["nodes"] is JsonArray && !c.ContainsKey("pageInfo") ? c : null;
        if (child.Arguments.Count == 0)
        {
            return connection is null ? value : Page(connection, int.MaxValue, orderBy: null);
        }

        if (connection is null || child.Arguments.Keys.Any(static k => k is not ("first" or "orderBy")) || First(child) is not { } count)
        {
            throw FakeRequestException.Invalid($"At {path} the fake Linear server reads only 'first' (a positive Int) and 'orderBy' (updatedAt or createdAt) on a nested connection.");
        }

        return Page(connection, count, OrderBy(child, path));
    }

    // why: the field name of the recording to sort by; Linear's nested orderBy is an enum, so it comes through as a bare word
    private static string? OrderBy(GraphQlField child, string path) =>
        child.Arguments.GetValueOrDefault("orderBy") switch
        {
            null => null,
            JsonValue v when v.TryGetValue<string>(out var by) && (string.Equals(by, "updatedAt", StringComparison.Ordinal) || string.Equals(by, "createdAt", StringComparison.Ordinal)) => by,
            var other => throw FakeRequestException.Invalid($"At {path} 'orderBy' must be updatedAt or createdAt, got {other?.ToJsonString()}."),
        };

    private static int? First(GraphQlField child) =>
        child.Arguments.GetValueOrDefault("first") is JsonValue n && n.TryGetValue<int>(out var count) && count > 0 ? count : null;

    // A nested connection in the recordings holds every child and no pageInfo; this gives it the first 'count' nodes
    // (newest first by 'orderBy' when it is given) and the pageInfo a client pages with.
    private static JsonObject Page(JsonObject connection, int count, string? orderBy)
    {
        var nodes = connection["nodes"]!.AsArray(); // why: Nested passes only connections whose 'nodes' is an array
        IEnumerable<JsonNode?> ordered = orderBy is null ? nodes : nodes.OrderByDescending(n => (string?)n?[orderBy], StringComparer.Ordinal);
        var kept = ordered.Take(count).Select(x => x?.DeepClone()).ToList();
        var page = (JsonObject)connection.DeepClone();
        page["nodes"] = new JsonArray([.. kept]);
        page["pageInfo"] = new JsonObject
        {
            ["hasNextPage"] = kept.Count < nodes.Count,
            ["hasPreviousPage"] = false,
            ["startCursor"] = (string?)kept.FirstOrDefault()?["id"],
            ["endCursor"] = (string?)kept.LastOrDefault()?["id"],
        };
        return page;
    }
}
