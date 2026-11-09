using System.Text.Json;
using Aw.Kernel;

namespace Aw.Sindri.Core.Toolkits;

/// <summary>What <c lang="text">&lt;toolkit&gt; --manifest</c> prints (toolkit.json): name, version, resources and tools with their schemas.</summary>
/// <param name="Name">The toolkit name.</param>
/// <param name="Version">The toolkit version.</param>
/// <param name="Resources">The declared resources.</param>
/// <param name="Tools">The tools.</param>
public sealed record ToolkitManifest(string Name, string Version, IReadOnlyList<ToolkitResource> Resources, IReadOnlyList<ToolkitTool> Tools)
{
    /// <summary>Reads a manifest.</summary>
    /// <param name="json">The manifest text.</param>
    /// <returns>The manifest, or SND-TK-003 when it isn't the SDK's shape or a name can't become a client tool name.</returns>
    public static Result<ToolkitManifest> Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var name = Text(root, "name");
            var version = Text(root, "version");
            if (name is null || version is null || !Array(root, "resources", out var resources) || !Array(root, "tools", out var tools))
            {
                return Bad("it needs name, version, resources and tools");
            }

            var parsedResources = ReadResources(resources);
            var parsedTools = ReadTools(name, tools);
            return parsedResources.IsError ? parsedResources.Error
                : parsedTools.IsError ? parsedTools.Error
                : new ToolkitManifest(name, version, parsedResources.Value, parsedTools.Value);
        }
        catch (JsonException e)
        {
            return Bad(e.Message);
        }
    }

    private static Result<IReadOnlyList<ToolkitResource>> ReadResources(JsonElement resources)
    {
        List<ToolkitResource> parsed = [];
        foreach (var r in resources.EnumerateArray())
        {
            var kind = Text(r, "kind");
            var name = Text(r, "name");
            var variable = Text(r, "variable");
            if (kind is null || name is null || variable is null)
            {
                return Bad("a resource needs kind, name and variable");
            }

            parsed.Add(new ToolkitResource(kind, name, Text(r, "size"), variable));
        }

        return parsed;
    }

    private static Result<IReadOnlyList<ToolkitTool>> ReadTools(string toolkit, JsonElement tools)
    {
        List<ToolkitTool> parsed = [];
        foreach (var t in tools.EnumerateArray())
        {
            var name = Text(t, "name");
            var description = Text(t, "description");
            if (name is null || description is null || !t.TryGetProperty("inputSchema", out var input) || !t.TryGetProperty("outputSchema", out var output))
            {
                return Bad("a tool needs name, description, inputSchema and outputSchema");
            }

            if (ToolNames.For(toolkit, name) is { IsError: true } bad)
            {
                return Bad(bad.Error.Message);
            }

            if (parsed.Exists(p => string.Equals(p.Name, name, StringComparison.Ordinal)))
            {
                return Bad($"the tool '{name}' is listed twice");
            }

            parsed.Add(new ToolkitTool(name, description, input.GetRawText(), output.GetRawText()));
        }

        return parsed;
    }

    private static bool Array(JsonElement element, string property, out JsonElement array) =>
        element.TryGetProperty(property, out array) && array.ValueKind == JsonValueKind.Array;

    private static string? Text(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static Error Bad(string why) => SindriErrors.ToolkitManifestInvalid.With($"toolkit.json isn't valid: {why}.");
}
