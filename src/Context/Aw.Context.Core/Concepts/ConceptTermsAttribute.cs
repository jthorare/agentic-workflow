using System.Text.Json;
using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Concepts;

/// <summary>Reads the glossary terms a build stored in a concept node's attributes ('terms', a list of words).</summary>
internal static class ConceptTermsAttribute
{
    internal static List<string> Of(GraphNode concept) =>
        concept.Attributes is { ValueKind: JsonValueKind.Object } attributes && attributes.TryGetProperty("terms", out var terms) && terms.ValueKind == JsonValueKind.Array
            ? [.. terms.EnumerateArray().Where(t => t.ValueKind == JsonValueKind.String).Select(t => t.GetString()).OfType<string>()]
            : [];
}
