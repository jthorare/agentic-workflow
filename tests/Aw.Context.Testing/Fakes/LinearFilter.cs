using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aw.Context.Testing.Fakes;

/// <summary>
/// Evaluates a Linear filter object (IssueFilter, ProjectFilter, ...) against a recorded node: nested fields
/// ('team: { key: { eq: "SPL" } }'), 'and' / 'or', 'some' / 'every' over a connection, and the comparators
/// eq, neq, in, nin, gt, gte, lt, lte, null, eqIgnoreCase, contains, containsIgnoreCase, startsWith. Dates compare as
/// instants; comparing a date field with something that isn't a date, a field the recordings don't have, or a field
/// filter straight on a connection is refused, never guessed; so is a null or non-scalar operand (use 'null' for null).
/// A null field or relation fails every comparator but 'neq', 'nin' and 'null: true' (the fake doesn't model Linear's
/// joins).
/// </summary>
internal static class LinearFilter
{
    private static readonly HashSet<string> _comparators = new(StringComparer.Ordinal)
    {
        "eq", "neq", "in", "nin", "gt", "gte", "lt", "lte", "null", "eqIgnoreCase", "contains", "containsIgnoreCase", "startsWith",
    };

    // Every clause is evaluated before the results are combined (no short-circuit), so a refusal never depends on
    // whether an earlier clause already failed: a bad filter is a 400 whatever its order.
    public static bool Matches(JsonNode? node, JsonObject filter) => Every([.. filter.Select(entry => Entry(node, entry.Key, entry.Value))]);

    private static bool Entry(JsonNode? node, string key, JsonNode? condition) => key switch
    {
        "and" => Every([.. List(key, condition).Select(f => Matches(node, Object(key, f)))]),
        "or" => Some([.. List(key, condition).Select(f => Matches(node, Object(key, f)))]),
        "some" => Some([.. Items(key, node).Select(item => Matches(item, Object(key, condition)))]),
        "every" => Every([.. Items(key, node).Select(item => Matches(item, Object(key, condition)))]),
        _ when _comparators.Contains(key) => Compare(node, key, condition),
        _ => Field(node, key, condition),
    };

    private static bool Field(JsonNode? node, string key, JsonNode? condition)
    {
        var filter = Object(key, condition);
        if (node is null)
        {
            return Matches(node: null, filter);
        }

        if (node is not JsonObject o)
        {
            throw FakeRequestException.Invalid($"Filter field '{key}' is applied to a scalar value; compare it with eq, in, gt and the like.");
        }

        if (!o.TryGetPropertyValue(key, out var value))
        {
            throw FakeRequestException.Invalid(o.ContainsKey("nodes")
                ? $"Filter '{key}' is on a connection; wrap it in 'some' or 'every'."
                : $"Filter field '{key}' isn't in the recorded nodes (fields here: {string.Join(", ", o.Select(p => p.Key))}).");
        }

        return Matches(value, filter);
    }

    private static bool Compare(JsonNode? value, string comparator, JsonNode? operand) => comparator switch
    {
        "null" => operand is JsonValue flag && flag.TryGetValue<bool>(out var want)
            ? (value is null) == want
            : throw FakeRequestException.Invalid("Filter 'null' takes a Boolean."),
        "eq" => Equal(value, operand, comparator),
        "neq" => !Equal(value, operand, comparator),
        "in" => Some([.. List(comparator, operand).Select(o => Equal(value, o, comparator))]),
        "nin" => !Some([.. List(comparator, operand).Select(o => Equal(value, o, comparator))]),
        "gt" or "gte" or "lt" or "lte" => Scalar(comparator, operand) is var checkedOperand && value is not null && Holds(comparator, Order(value, checkedOperand)),
        _ => Text(comparator, value, operand),
    };

    private static bool Holds(string comparator, int order) => comparator switch
    {
        "gt" => order > 0,
        "gte" => order >= 0,
        "lt" => order < 0,
        _ => order <= 0,
    };

    private static bool Text(string comparator, JsonNode? value, JsonNode? operand)
    {
        if (operand?.GetValueKind() != JsonValueKind.String || (value is not null && value.GetValueKind() != JsonValueKind.String))
        {
            throw FakeRequestException.Invalid($"Filter '{comparator}' compares strings; got '{value?.ToJsonString()}' and '{operand?.ToJsonString() ?? "null"}'.");
        }

        if (value is null)
        {
            return false;
        }

        var (left, right) = (value.GetValue<string>(), operand.GetValue<string>());
        return comparator switch
        {
            "eqIgnoreCase" => string.Equals(left, right, StringComparison.OrdinalIgnoreCase),
            "contains" => left.Contains(right, StringComparison.Ordinal),
            "containsIgnoreCase" => left.Contains(right, StringComparison.OrdinalIgnoreCase),
            _ => left.StartsWith(right, StringComparison.Ordinal),
        };
    }

    // A null operand is refused (Linear's meaning for it isn't modeled; the 'null' comparator says it), and the operand
    // is checked before the field value, so a refusal never depends on whether the recorded value is null.
    private static bool Equal(JsonNode? value, JsonNode? operand, string comparator)
    {
        var checkedOperand = Scalar(comparator, operand);
        return value is not null && Order(value, checkedOperand) == 0;
    }

    private static JsonValue Scalar(string comparator, JsonNode? operand) =>
        operand is JsonValue v && v.GetValueKind() is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False
            ? v
            : throw FakeRequestException.Invalid(operand is null
                ? $"Filter '{comparator}' compares with null; use the 'null' comparator."
                : $"Filter '{comparator}' takes a string, number or Boolean, not '{operand.ToJsonString()}'.");

    private static int Order(JsonNode value, JsonValue operand)
    {
        if (Instant(value) is { } left)
        {
            return Instant(operand) is { } right
                ? left.CompareTo(right)
                : throw FakeRequestException.Invalid($"Filter compares a date with '{operand.ToJsonString()}', which isn't an ISO-8601 instant (the fake reads no durations).");
        }

        return (value.GetValueKind(), operand.GetValueKind()) switch
        {
            (JsonValueKind.Number, JsonValueKind.Number) => value.GetValue<double>().CompareTo(operand.GetValue<double>()),
            (JsonValueKind.String, JsonValueKind.String) => string.CompareOrdinal(value.GetValue<string>(), operand.GetValue<string>()),
            (JsonValueKind.True or JsonValueKind.False, JsonValueKind.True or JsonValueKind.False) => value.GetValue<bool>().CompareTo(operand.GetValue<bool>()),
            _ => throw FakeRequestException.Invalid($"Filter compares '{value.ToJsonString()}' with '{operand.ToJsonString()}', values of different types."),
        };
    }

    private static DateTimeOffset? Instant(JsonNode node) =>
        node.GetValueKind() == JsonValueKind.String && node.GetValue<string>() is { Length: >= 10 } s && s[4] == '-'
            && DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at)
            ? at
            : null;

    private static JsonArray Items(string key, JsonNode? node) => node switch
    {
        null => [],
        JsonArray array => array,
        JsonObject { } connection when connection["nodes"] is JsonArray nodes => nodes,
        _ => throw FakeRequestException.Invalid($"Filter '{key}' takes a connection or a list, not '{node.ToJsonString()}'."),
    };

    private static bool Every(List<bool> results) => results.TrueForAll(r => r);

    private static bool Some(List<bool> results) => results.Exists(r => r);

    private static JsonArray List(string key, JsonNode? node) =>
        node as JsonArray ?? throw FakeRequestException.Invalid($"Filter '{key}' takes a list.");

    private static JsonObject Object(string key, JsonNode? node) =>
        node as JsonObject ?? throw FakeRequestException.Invalid($"Filter '{key}' takes an object, found '{node?.ToJsonString() ?? "null"}'.");
}
