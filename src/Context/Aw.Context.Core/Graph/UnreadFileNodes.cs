using System.Text.Json;

namespace Aw.Context.Core.Graph;

/// <summary>The code: nodes the build makes for tracked files the extractor doesn't read (a script, a config), so a record's mention of one keeps its edge. They carry the attribute read: false and implement nothing.</summary>
public static class UnreadFileNodes
{
    /// <summary>The attribute that marks a file node the extractor didn't read.</summary>
    public const string Attribute = "read";

    /// <summary>The attributes of an unread file node: {"read": false}.</summary>
    /// <returns>The attributes object.</returns>
    public static JsonElement Attributes()
    {
        using var document = JsonDocument.Parse("{\"" + Attribute + "\":false}");
        return document.RootElement.Clone();
    }

    /// <summary>Whether <paramref name="node"/> is a code: node the extractor didn't read.</summary>
    /// <param name="node">A node.</param>
    /// <returns>True for a code: node with read: false.</returns>
    public static bool Is(GraphNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return node.Key.Kind == NodeKind.Code
            && node.Attributes is { ValueKind: JsonValueKind.Object } attributes
            && attributes.TryGetProperty(Attribute, out var read)
            && read.ValueKind == JsonValueKind.False;
    }
}
