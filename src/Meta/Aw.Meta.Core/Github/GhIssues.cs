using System.Globalization;
using System.Text.Json;

namespace Aw.Meta.Core.Github;

/// <summary>Reads the JSON `gh issue list` prints. Anything unexpected is "nothing", never an exception.</summary>
public static class GhIssues
{
    /// <summary>Reads `gh issue list --json number,title,state,closedAt,url`.</summary>
    /// <param name="json">The output.</param>
    /// <returns>The issues, or null when the output is not a JSON array.</returns>
    public static IReadOnlyList<IssueRef>? List(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Array
                ? [.. document.RootElement.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.Object && item.TryGetProperty("number", out var number) && number.TryGetInt32(out _))
                    .Select(static item => new IssueRef(item.GetProperty("number").GetInt32(), Text(item, "title"), string.Equals(Text(item, "state"), "OPEN", StringComparison.OrdinalIgnoreCase), Closed(item), Text(item, "url")))]
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // gh prints a never-set time as year 1, which is no time either.
    private static DateTimeOffset? Closed(JsonElement item) =>
        DateTimeOffset.TryParse(Text(item, "closedAt"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var time) && time.Year > 1 ? time : null;

    private static string Text(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
}
