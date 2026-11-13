using System.Text.Json.Nodes;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>The prompt block's guard against the model copying the output schema as its answer (#3588): the example answer in the instruction, and the check that spots a copy.</summary>
public sealed partial class PromptBlock
{
    private static readonly HashSet<string> _schemaKeywords = new(["type", "properties", "required"], StringComparer.Ordinal);

    // why: the required fields show what to write (all fields when none is required); a non-text placeholder is not valid JSON, so a copied example fails the parse and gets the repair ask
    private static string ExampleAnswer(JsonObject schema)
    {
        if (schema["properties"] is not JsonObject properties)
        {
            return "{}";
        }

        var required = Required(schema);
        var shown = properties
            .Where(p => required.Count == 0 || required.Contains(p.Key))
            .Select(static p => $"{Quoted(p.Key)}: {Placeholder(p.Key, TypeOf(p.Value).Name)}");
        return "{" + string.Join(", ", shown) + "}";
    }

    private static string Placeholder(string name, string? type) => type switch
    {
        "boolean" => "<true or false>",
        "number" or "integer" => "<a number>",
        "array" => "[<items>]",
        "object" => "{<fields>}",
        _ => Quoted($"<your {name}>"),
    };

    // why: the default JSON writer escapes the angle brackets as unicode escapes, so the model saw escape codes instead of the placeholder; only the quote and the backslash need escaping inside a JSON string here
    private static string Quoted(string text) => "\"" + text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    // why: a JSON-schema-shaped object holding none of the schema's own fields is the model copying the schema, not an answer (#3588); 'type', 'properties' and 'required' are left out of the comparison because the copy always has them
    private static bool IsSchemaEcho(JsonObject answer, JsonObject schema) =>
        string.Equals(Text(answer["type"]), "object", StringComparison.Ordinal)
        && answer["properties"] is JsonObject
        && !(schema["properties"] is JsonObject properties
            && properties.Any(p => !_schemaKeywords.Contains(p.Key) && answer.ContainsKey(p.Key)));
}
