using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>
/// The check a 'run_agent' node's report goes through (PR 7): every output the node declares must be there, in its declared type, and nothing else. A string output is the file's text;
/// any other type is a JSON value. A refusal names the field, so the agent can fix it and report again.
/// </summary>
public static class RunAgentReport
{
    /// <summary>Checks a report against the declared outputs.</summary>
    /// <param name="declared">The declared output fields with their types ('string', 'number', 'boolean', 'object', 'array'; a trailing '?' is optional).</param>
    /// <param name="reported">What the agent reported: each output's content by field name.</param>
    /// <returns>The values by field; SND-INPUT naming the first missing, undeclared or mistyped field.</returns>
    public static Result<IReadOnlyDictionary<string, JsonNode?>> Check(IReadOnlyDictionary<string, string> declared, IReadOnlyDictionary<string, ReadOnlyMemory<byte>> reported)
    {
        ArgumentNullException.ThrowIfNull(declared);
        ArgumentNullException.ThrowIfNull(reported);
        foreach (var name in reported.Keys.Order(StringComparer.Ordinal))
        {
            if (!declared.ContainsKey(name))
            {
                return Refused($"'{name}' is not an output this node declares (it declares: {(declared.Count == 0 ? "none" : string.Join(", ", declared.Keys.Order(StringComparer.Ordinal)))}); report only those");
            }
        }

        var values = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        foreach (var (name, type) in declared.OrderBy(static d => d.Key, StringComparer.Ordinal))
        {
            var optional = type.EndsWith('?');
            var kind = optional ? type[..^1] : type;
            if (!reported.TryGetValue(name, out var content))
            {
                if (!optional)
                {
                    return Refused($"the report is missing the declared output '{name}' ({kind}); write it to .sindri-out/{name} and add --output fact:{name}=<that file>");
                }

                values[name] = null;
                continue;
            }

            var read = Read(name, kind, Encoding.UTF8.GetString(content.Span));
            if (read.IsError)
            {
                return read.Error;
            }

            values[name] = read.Value;
        }

        return values;
    }

    private static Result<JsonNode?> Read(string name, string kind, string text)
    {
        if (string.Equals(kind, "string", StringComparison.Ordinal))
        {
            return JsonValue.Create(text.TrimEnd('\r', '\n'));
        }

        try
        {
            var node = JsonNode.Parse(text);
            return node is not null && Fits(node, kind) ? node : Refused($"the output '{name}' must be {Article(kind)} as JSON; fix the file and report again");
        }
        catch (JsonException)   // boundary: text that is not JSON is the same refusal as JSON of the wrong type
        {
            return Refused($"the output '{name}' must be {Article(kind)} as JSON, and its file is not JSON; fix the file and report again");
        }
    }

    private static bool Fits(JsonNode node, string kind) => kind switch
    {
        "number" => node.GetValueKind() == JsonValueKind.Number,
        "boolean" => node.GetValueKind() is JsonValueKind.True or JsonValueKind.False,
        "object" => node is JsonObject,
        "array" => node is JsonArray,
        _ => true,
    };

    private static string Article(string kind) => kind switch
    {
        "object" => "an object",
        "array" => "an array",
        _ => "a " + kind,
    };

    private static Error Refused(string reason) => SindriErrors.InvalidInput.With(reason);
}
