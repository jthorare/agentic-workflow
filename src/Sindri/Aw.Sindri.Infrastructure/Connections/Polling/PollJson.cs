using System.Globalization;
using System.Text.Json;

namespace Aw.Sindri.Infrastructure.Connections.Polling;

/// <summary>Small readers over provider JSON: a missing or mistyped member reads as null, never throws.</summary>
internal static class PollJson
{
    private const int MaxText = 2000;

    internal static JsonElement? Member(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var found) && found.ValueKind != JsonValueKind.Null ? found : null;

    internal static JsonElement? Path(JsonElement element, string first, string second)
        => Member(element, first) is { } inner ? Member(inner, second) : null;

    internal static string? Text(JsonElement? element)
    {
        var text = element switch
        {
            { ValueKind: JsonValueKind.String } e => e.GetString(),
            { ValueKind: JsonValueKind.Number } e => e.GetRawText(),
            _ => null,
        };
        return text is { Length: > MaxText } ? text[..MaxText] : text;
    }

    internal static DateTimeOffset? Time(string? text)
        => DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed) ? parsed : null;

    internal static void Put(Dictionary<string, string> fields, string name, string? value)
    {
        if (value is not null)
        {
            fields[name] = value;
        }
    }
}
