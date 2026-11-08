using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Aw.Sindri.Core.Graph.Interpolation;

/// <summary>
/// '{{node.field}}' in config strings (issue 2258, Part 2 "References"). A string that is exactly one reference keeps the
/// referenced value's type; inside other text a value renders as itself, objects and arrays as compact JSON and null as nothing.
/// </summary>
public static partial class Template
{
    private static readonly JsonSerializerOptions _compact = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>The references in <paramref name="text"/>, in order, trimmed ('{{ a.b }}' is 'a.b').</summary>
    /// <param name="text">A config string.</param>
    /// <returns>The references; empty when there are none.</returns>
    public static IReadOnlyList<string> References(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return [.. Reference.Matches(text).Select(static m => m.Groups["ref"].Value)];
    }

    /// <summary>Renders <paramref name="text"/>.</summary>
    /// <param name="text">A config string.</param>
    /// <param name="lookup">The value of a reference such as 'agent.drifted'; null for a missing or null value.</param>
    /// <returns>The referenced value itself when <paramref name="text"/> is exactly one reference; otherwise the text as a string value.</returns>
    public static JsonNode? Render(string text, Func<string, JsonNode?> lookup)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(lookup);
        var whole = Reference.Match(text);
        if (whole.Success && whole.Index == 0 && whole.Length == text.Length)
        {
            return lookup(whole.Groups["ref"].Value)?.DeepClone();
        }

        return JsonValue.Create(Reference.Replace(text, m => AsText(lookup(m.Groups["ref"].Value))));
    }

    private static string AsText(JsonNode? value) => value switch
    {
        null => string.Empty,
        JsonValue v when v.GetValueKind() == JsonValueKind.String => v.GetValue<string>(),
        _ => value.ToJsonString(_compact),
    };

    [GeneratedRegex("\\{\\{\\s*(?<ref>[^{}]*?)\\s*\\}\\}", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Reference { get; }
}
