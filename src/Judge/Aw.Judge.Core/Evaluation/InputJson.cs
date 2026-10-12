using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aw.Judge.Core.Evaluation;

/// <summary>How a question input is read and written for the digest and for storage: compact JSON through the AwJson profile.</summary>
public static class InputJson
{
    private static readonly JsonDocumentOptions _strict = new() { AllowDuplicateProperties = false };

    /// <summary>
    /// Parses text as JSON that can be read and written back. A syntax error, a repeated key and an escape that isn't
    /// valid UTF-16 (a lone surrogate such as \ud800, in a value or a key) all fail: JsonNode.Parse accepts the last
    /// two and throws later, when something enumerates the node, reads a string or writes it.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="node">The value (null for JSON null, and when the text isn't such JSON).</param>
    /// <returns>Whether the text is such JSON.</returns>
    public static bool TryParse(string text, out JsonNode? node)
    {
        try
        {
            node = JsonNode.Parse(text, documentOptions: _strict);
            Touch(node);
            return true;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            node = null;
            return false;
        }
    }

    // Reads every key and every string once, so whatever can't be unescaped throws here and not in a later reader.
    // The parser's depth limit (64) bounds the recursion.
    private static void Touch(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (_, value) in obj)
                {
                    Touch(value);
                }

                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    Touch(item);
                }

                break;
            case JsonValue value when value.GetValueKind() == JsonValueKind.String:
                _ = value.GetValue<string>();
                break;
            default:
                break;
        }
    }

    /// <summary>The compact JSON text of a node ('null' for JSON null).</summary>
    /// <param name="node">The node.</param>
    /// <param name="options">The package's AwJson options (relaxed escaping, not indented).</param>
    /// <returns>The text.</returns>
    public static string Write(JsonNode? node, JsonSerializerOptions options) => node?.ToJsonString(options) ?? "null";

    /// <summary>The input digest: the first 16 lowercase hex characters of the SHA-256 of <see cref="Write"/> as UTF-8.</summary>
    /// <param name="node">The input, unredacted.</param>
    /// <param name="options">The package's AwJson options.</param>
    /// <returns>The digest.</returns>
    public static string Digest(JsonNode? node, JsonSerializerOptions options) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Write(node, options))))[..16];
}
