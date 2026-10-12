using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aw.Context.Testing.Fakes;

/// <summary>Answers one root field from the recordings: a filtered, ordered, paged connection, or one node by id, cut to the selection.</summary>
internal static class LinearResolver
{
    private const int DefaultPage = 50;
    private const int MaxPage = 250;
    private static readonly HashSet<string> _connectionArguments = new(StringComparer.Ordinal) { "filter", "first", "after", "orderBy", "includeArchived" };
    private static readonly HashSet<string> _singleArguments = new(StringComparer.Ordinal) { "id" };

    /// <summary>The field's value, or the GraphQL error message for a node that isn't there.</summary>
    public static (JsonNode? Value, string? Error) Resolve(GraphQlField field)
    {
        if (LinearRecordings.Connections.ContainsKey(field.Name))
        {
            return (LinearShape.Select(Connection(field), field, field.Name), null);
        }

        if (LinearRecordings.Singles.TryGetValue(field.Name, out var connection))
        {
            return Single(field, connection);
        }

        throw FakeRequestException.Invalid(
            $"Cannot query field \"{field.Name}\" on type \"Query\". The fake Linear server answers only: {LinearRecordings.Supported}.");
    }

    private static (JsonNode? Value, string? Error) Single(GraphQlField field, string connection)
    {
        Known(field, _singleArguments);
        var id = Text(field, "id") ?? throw FakeRequestException.Invalid($"Field \"{field.Name}\" needs a string 'id' argument.");
        var node = LinearRecordings.Nodes(connection).Find(n =>
            (string?)n["id"] == id || string.Equals((string?)n["identifier"], id, StringComparison.OrdinalIgnoreCase));
        var type = char.ToUpperInvariant(field.Name[0]) + field.Name[1..];
        return node is null
            ? (null, $"Entity not found: {type} - Could not find referenced {type} '{id}'.")
            : (LinearShape.Select(node, field, field.Name), null);
    }

    private static JsonObject Connection(GraphQlField field)
    {
        Known(field, _connectionArguments);
        var filter = field.Arguments.GetValueOrDefault("filter") switch
        {
            null => null,
            JsonObject o => o,
            _ => throw FakeRequestException.Invalid("'filter' takes an object."),
        };
        var order = Text(field, "orderBy") ?? "createdAt";
        if (order is not ("createdAt" or "updatedAt"))
        {
            throw FakeRequestException.Invalid($"orderBy '{order}' isn't a PaginationOrderBy; use createdAt or updatedAt.");
        }

        var nodes = LinearRecordings.Nodes(field.Name)
            .Where(n => filter is null || LinearFilter.Matches(n, filter))
            .OrderBy(n => (string?)n[order], StringComparer.Ordinal)
            .ThenBy(n => (string?)n["id"], StringComparer.Ordinal)
            .ToList();
        return Page(field, nodes);
    }

    private static JsonObject Page(GraphQlField field, List<JsonNode> nodes)
    {
        var after = Text(field, "after");
        var start = after is null ? 0 : nodes.FindIndex(n => (string?)n["id"] == after) + 1;
        if (start == 0 && after is not null)
        {
            throw FakeRequestException.Invalid($"Cursor '{after}' doesn't name a node of \"{field.Name}\" under this filter and order.");
        }

        var first = field.Arguments.GetValueOrDefault("first") switch
        {
            null => DefaultPage,
            JsonValue f when f.TryGetValue<int>(out var n) => n,
            JsonValue f when f.GetValueKind() == JsonValueKind.Number && int.TryParse(f.ToJsonString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) => n,
            var other => throw FakeRequestException.Invalid($"'first' takes an Int, got {other.ToJsonString()}."),
        };
        if (first is < 1 or > MaxPage)
        {
            throw FakeRequestException.Invalid(string.Create(CultureInfo.InvariantCulture, $"'first' must be 1 to {MaxPage}, got {first}."));
        }

        var page = nodes.Skip(start).Take(first).ToList();
        return new JsonObject
        {
            ["nodes"] = new JsonArray([.. page]),
            ["pageInfo"] = new JsonObject
            {
                ["hasNextPage"] = start + page.Count < nodes.Count,
                ["hasPreviousPage"] = start > 0,
                ["startCursor"] = (string?)page.FirstOrDefault()?["id"],
                ["endCursor"] = (string?)page.LastOrDefault()?["id"],
            },
        };
    }

    private static string? Text(GraphQlField field, string name) =>
        field.Arguments.GetValueOrDefault(name) switch
        {
            null => null,
            JsonValue value when value.GetValueKind() == JsonValueKind.String => value.GetValue<string>(),
            var other => throw FakeRequestException.Invalid($"Argument '{name}' on \"{field.Name}\" takes a string or enum value, got {other.ToJsonString()}."),
        };

    private static void Known(GraphQlField field, HashSet<string> allowed)
    {
        var unknown = field.Arguments.Keys.FirstOrDefault(a => !allowed.Contains(a));
        if (unknown is not null)
        {
            throw FakeRequestException.Invalid(
                $"Unknown argument \"{unknown}\" on field \"{field.Name}\"; the fake Linear server reads {string.Join(", ", allowed)}.");
        }
    }
}
