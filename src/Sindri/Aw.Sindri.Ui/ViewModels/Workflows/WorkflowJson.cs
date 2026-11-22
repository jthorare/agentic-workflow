using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>A stored JSON value as the text the page shows.</summary>
internal static class WorkflowJson
{
    // the code block's config field (CodeBlock in Aw.Sindri.Core)
    private const string CodeField = "code";

    /// <summary>A value as indented JSON (written by hand, so no serializer metadata is needed); a string as itself; empty for none.</summary>
    /// <param name="value">The served value.</param>
    /// <returns>The text.</returns>
    internal static string Pretty(JsonElement? value)
    {
        if (value is not { } element || element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return string.Empty;
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            return element.GetString() ?? string.Empty;
        }

        return Write(element.WriteTo);
    }

    /// <summary>
    /// A step's input with its code body taken out: an object with a string "code" (a code step's interpolated config) gives that body as written, with real line breaks,
    /// and the rest of the object as indented JSON (empty when the body is all there is). Any other input gives no code and <see cref="Pretty"/>'s text.
    /// </summary>
    /// <param name="value">The served input.</param>
    /// <returns>The code body, or empty, and the rest as the page shows it.</returns>
    internal static (string Code, string Input) CodeAndRest(JsonElement? value)
    {
        if (value is not { ValueKind: JsonValueKind.Object } element || !element.TryGetProperty(CodeField, out var code) || code.ValueKind != JsonValueKind.String)
        {
            return (string.Empty, Pretty(value));
        }

        var rest = element.EnumerateObject().Where(static p => !string.Equals(p.Name, CodeField, StringComparison.Ordinal)).ToList();
        var text = rest.Count == 0 ? string.Empty : Write(writer =>
        {
            writer.WriteStartObject();
            foreach (var property in rest)
            {
                property.WriteTo(writer);
            }

            writer.WriteEndObject();
        });
        return (code.GetString() ?? string.Empty, text);
    }

    private static string Write(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, NewLine = "\n", Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            write(writer);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
