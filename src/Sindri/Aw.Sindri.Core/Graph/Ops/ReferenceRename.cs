using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Aw.Sindri.Core.Graph.Ops;

/// <summary>Follows a node rename through the '{{old.field}}' references in a config.</summary>
internal static partial class ReferenceRename
{
    [GeneratedRegex("(?<open>\\{\\{\\s*)(?<id>[a-z][a-z0-9_]*)(?=[.\\s}])", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Reference { get; }

    // why: only '{{old.field}}' references follow; a bare name inside a condition's 'when' text is left for the validator to flag.
    public static void Apply(JsonNode? node, string old, string next)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var key in obj.Select(static p => p.Key).ToList())
                {
                    Visit(obj[key], text => obj[key] = JsonValue.Create(text), old, next);
                }

                break;
            case JsonArray list:
                for (var i = 0; i < list.Count; i++)
                {
                    var at = i;
                    Visit(list[at], text => list[at] = JsonValue.Create(text), old, next);
                }

                break;
            default:
                break;
        }
    }

    private static void Visit(JsonNode? item, Action<string> replace, string old, string next)
    {
        if (item is JsonValue value && value.TryGetValue<string>(out var text))
        {
            replace(Reference.Replace(text, m => string.Equals(m.Groups["id"].Value, old, StringComparison.Ordinal) ? m.Groups["open"].Value + next : m.Value));
            return;
        }

        Apply(item, old, next);
    }
}
