using System.Text.Json.Nodes;

namespace Aw.Sindri.Core.Graph.Interpolation;

/// <summary>
/// '{{node.field}}' in config strings (issue 2258, Part 2 "References"). A string that is exactly one reference keeps the
/// referenced value's type; inside other text a value renders as itself, objects and arrays as compact JSON and null as nothing.
/// </summary>
public static class Template
{
    /// <summary>The references in <paramref name="text"/>, in order, trimmed ('{{ a.b }}' is 'a.b').</summary>
    /// <param name="text">A config string.</param>
    /// <returns>The references; empty when there are none.</returns>
    public static IReadOnlyList<string> References(string text) => throw new NotSupportedException();

    /// <summary>Renders <paramref name="text"/>.</summary>
    /// <param name="text">A config string.</param>
    /// <param name="lookup">The value of a reference such as 'agent.drifted'; null for a missing or null value.</param>
    /// <returns>The referenced value itself when <paramref name="text"/> is exactly one reference; otherwise the text as a string value.</returns>
    public static JsonNode? Render(string text, Func<string, JsonNode?> lookup) => throw new NotSupportedException();
}
