using System.Text;
using System.Text.Json;
using System.Text.Encodings.Web;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>A stored JSON value as the text the page shows.</summary>
internal static class WorkflowJson
{
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

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, NewLine = "\n", Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            element.WriteTo(writer);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
