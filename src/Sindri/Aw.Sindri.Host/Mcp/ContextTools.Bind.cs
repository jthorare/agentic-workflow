using System.Text.Json;
using Aw.Context.Core;
using Aw.Kernel;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Mcp;

/// <summary>context_query's argument binding.</summary>
internal static partial class ContextTools
{
    /// <summary>The arguments as a request; SND-CONTEXT-022 for an unknown property or a wrong type. Values are left to the engine (006).</summary>
    private static Result<QueryContextRequest> Bind(IDictionary<string, JsonElement>? arguments)
    {
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        var numbers = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (name, value) in arguments ?? new Dictionary<string, JsonElement>(StringComparer.Ordinal))
        {
            if (!BindOne(name, value, texts, numbers))
            {
                return ContextErrors.McpArgumentsInvalid.With(Invalid(name, value));
            }
        }

        return new QueryContextRequest(Get(texts, "q"), Get(texts, "node"), Get(texts, "item"), Get(texts, "symbol"),
            numbers.TryGetValue("depth", out var depth) ? depth.ToString(System.Globalization.CultureInfo.InvariantCulture) : null, numbers.TryGetValue("budget", out var budget) ? budget.ToString(System.Globalization.CultureInfo.InvariantCulture) : null, Diff: Get(texts, "diff"), Hops: Get(texts, "hops"), MapVersion: Get(texts, "mapVersion"));
    }

    /// <summary>One argument into its map; false for an unknown name or a wrong type. An optional argument sent as null is absent.</summary>
    private static bool BindOne(string name, JsonElement value, Dictionary<string, string> texts, Dictionary<string, int> numbers)
    {
        var text = _texts.Contains(name, StringComparer.Ordinal);
        var number = _numbers.Contains(name, StringComparer.Ordinal);
        if (value.ValueKind == JsonValueKind.Null)
        {
            return text || number;
        }

        if (text && value.ValueKind == JsonValueKind.String)
        {
            texts[name] = value.GetString()!;   // why: a String kind always has a string
            return true;
        }

        if (number && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsed))
        {
            numbers[name] = parsed;
            return true;
        }

        return false;
    }

    private static string? Get(Dictionary<string, string> texts, string name) => texts.TryGetValue(name, out var text) ? text : null;

    private static string Invalid(string name, JsonElement value) =>
        _texts.Contains(name, StringComparer.Ordinal) || _numbers.Contains(name, StringComparer.Ordinal)
            ? $"'{name}' must be {(_texts.Contains(name, StringComparer.Ordinal) ? "a string" : "an integer")}, got {value.ValueKind.ToString().ToLowerInvariant()} (fix: see the tool's input schema)"
            : $"context_query has no argument '{name}' (fix: q, node, item, symbol, diff, depth, hops, mapVersion, budget, limit, offset)";
}
