using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aw.Sindri.Toolkit;

/// <summary>Writes toolkit.json: the name, version, resources and tools with their schemas, as indented JSON ending in a newline.</summary>
internal static class ManifestWriter
{
    private static readonly JsonWriterOptions _writerOptions = new()
    {
        Indented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        NewLine = "\n",
        IndentCharacter = ' ',
        IndentSize = 2,
    };

    public static string Write(string name, string version, IReadOnlyList<ResourceNeed> needs, IReadOnlyList<ToolDefinition> tools)
    {
        var manifest = new JsonObject
        {
            ["name"] = name,
            ["version"] = version,
            ["resources"] = new JsonArray([.. needs.Select(Resource)]),
            ["tools"] = new JsonArray([.. tools.Select(Tool)]),
        };

        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, _writerOptions))
        {
            manifest.WriteTo(writer);
        }

        return Encoding.UTF8.GetString(buffer.ToArray()) + "\n";
    }

    private static JsonNode Resource(ResourceNeed need)
    {
        var entry = new JsonObject
        {
            ["kind"] = need.Kind.ToString().ToLowerInvariant(),
            ["name"] = need.Name,
        };
        if (need.Size is not null)
        {
            entry["size"] = need.Size;
        }

        entry["variable"] = need.Variable;
        return entry;
    }

    private static JsonNode Tool(ToolDefinition tool) => new JsonObject
    {
        ["name"] = tool.Name,
        ["description"] = tool.Description,
        ["inputSchema"] = tool.InputSchema.DeepClone(),
        ["outputSchema"] = tool.OutputSchema.DeepClone(),
    };
}
