using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aw.Sindri.Core.Line;

/// <summary>Reads a packet's files as JSON trees: the files are model and code output held as text, so the rules read them by name and a field that is absent is simply absent.</summary>
public static class LineJson
{
    /// <summary>A string field, or null when the node is no object, the field is missing or it is not a string.</summary>
    /// <param name="node">The node.</param>
    /// <param name="name">The field.</param>
    /// <returns>The text or null.</returns>
    public static string? Str(JsonNode? node, string name) =>
        node is JsonObject obj && obj[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    /// <summary>An integer field, or null.</summary>
    /// <param name="node">The node.</param>
    /// <param name="name">The field.</param>
    /// <returns>The number or null.</returns>
    public static int? Number(JsonNode? node, string name) =>
        node is JsonObject obj && obj[name] is JsonValue value && value.TryGetValue<int>(out var number) ? number : null;

    /// <summary>A boolean field, or null.</summary>
    /// <param name="node">The node.</param>
    /// <param name="name">The field.</param>
    /// <returns>The flag or null.</returns>
    public static bool? Bool(JsonNode? node, string name) =>
        node is JsonObject obj && obj[name] is JsonValue value && value.TryGetValue<bool>(out var flag) ? flag : null;

    /// <summary>An array field, or null.</summary>
    /// <param name="node">The node.</param>
    /// <param name="name">The field.</param>
    /// <returns>The array or null.</returns>
    public static JsonArray? Arr(JsonNode? node, string name) => node is JsonObject obj ? obj[name] as JsonArray : null;

    /// <summary>An object field, or null.</summary>
    /// <param name="node">The node.</param>
    /// <param name="name">The field.</param>
    /// <returns>The object or null.</returns>
    public static JsonObject? Obj(JsonNode? node, string name) => node is JsonObject obj ? obj[name] as JsonObject : null;

    /// <summary>The non-null elements of an array field; none for anything else.</summary>
    /// <param name="node">The node.</param>
    /// <param name="name">The field.</param>
    /// <returns>The elements.</returns>
    public static IEnumerable<JsonNode> Items(JsonNode? node, string name) => Items(Arr(node, name));

    /// <summary>The non-null elements of an array node; none for anything else.</summary>
    /// <param name="array">The array node.</param>
    /// <returns>The elements.</returns>
    public static IEnumerable<JsonNode> Items(JsonNode? array) => array is JsonArray items ? items.OfType<JsonNode>() : [];

    /// <summary>The string elements of an array field.</summary>
    /// <param name="node">The node.</param>
    /// <param name="name">The field.</param>
    /// <returns>The strings.</returns>
    public static IEnumerable<string> Strings(JsonNode? node, string name) => Strings(Arr(node, name));

    /// <summary>The string elements of an array node; none for anything else.</summary>
    /// <param name="array">The array node.</param>
    /// <returns>The strings.</returns>
    public static IEnumerable<string> Strings(JsonNode? array) =>
        Items(array).Select(static n => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null).OfType<string>();

    /// <summary>The tree as the text a packet file holds: UTF-8 JSON, two-space indented, relaxed escaping, ending with a newline.</summary>
    /// <param name="node">The tree.</param>
    /// <returns>The text.</returns>
    public static string Write(JsonNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true, NewLine = "\n", Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            node.WriteTo(writer);
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan) + "\n";
    }

    /// <summary>The text of a file whose tree was edited: the original when the edit changed nothing (so its bytes, and every hash over them, stay), else the tree written.</summary>
    /// <param name="original">The text the tree was read from.</param>
    /// <param name="edited">The edited tree.</param>
    /// <returns>The text to keep.</returns>
    public static string Rewritten(string original, JsonNode edited)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(edited);
        try
        {
            return JsonNode.DeepEquals(JsonNode.Parse(original), edited) ? original : Write(edited);
        }
        catch (JsonException)
        {
            return Write(edited);   // boundary: the original was not JSON, so there is nothing to keep
        }
    }

    /// <summary>A text with exactly one trailing newline, as every packet file ends.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The same text, newline-terminated.</returns>
    public static string Terminated(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.EndsWith('\n') ? text : text + "\n";
    }

    /// <summary>The 'sha256:' hash of a file's bytes (the convention index.json's acceptance chain uses).</summary>
    /// <param name="text">The file's text.</param>
    /// <returns>The hash.</returns>
    public static string Hash(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }

    /// <summary>Adds a node to an array (the non-generic overload: the generic one is reflection-based and not trimmable).</summary>
    /// <param name="array">The array.</param>
    /// <param name="node">The node to add.</param>
    public static void Append(JsonArray array, JsonNode node)
    {
        ArgumentNullException.ThrowIfNull(array);
        array.Add(node);
    }

    /// <summary>A string array as a JSON array.</summary>
    /// <param name="values">The strings.</param>
    /// <returns>The array.</returns>
    public static JsonArray StringArray(IEnumerable<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return new JsonArray([.. values.Select(static v => (JsonNode?)JsonValue.Create(v))]);
    }
}
