using System.Text.Json;

namespace Aw.Context.Core.Graph;

/// <summary>Reads a node's attributes.</summary>
internal static class NodeAttributes
{
    /// <summary>The longest a concept's summary is shown, cut on a word (context_concepts and the scoping graph).</summary>
    internal const int SummaryMax = 300;

    /// <summary>The string attribute <paramref name="name"/> of a node, or null when the node has no attributes or the attribute is missing or not a string.</summary>
    internal static string? String(GraphNode node, string name) =>
        node.Attributes is { ValueKind: JsonValueKind.Object } attributes && attributes.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
