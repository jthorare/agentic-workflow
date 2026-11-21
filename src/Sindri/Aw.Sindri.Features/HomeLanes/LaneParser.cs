using System.Globalization;
using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core;

namespace Aw.Sindri.Features.HomeLanes;

/// <summary>Reads the JSON of 'aw-meta status', 'lanes-context' and 'usage' into a <see cref="LaneSnapshot"/>. Keys match case-insensitively; a key it does not know is ignored.</summary>
internal static class LaneParser
{
    /// <summary>Parses the three outputs.</summary>
    /// <param name="status">'aw-meta status --json' (mergeQueue[]).</param>
    /// <param name="context">'aw-meta lanes-context --json' (lanes[]).</param>
    /// <param name="usage">'aw-meta usage --json' (subjects[]).</param>
    /// <returns>The snapshot, or SND-LANES-001 when any output is not the JSON object it should be.</returns>
    public static Result<LaneSnapshot> Parse(string status, string context, string usage)
    {
        try
        {
            using var s = JsonDocument.Parse(status);
            using var c = JsonDocument.Parse(context);
            using var u = JsonDocument.Parse(usage);
            return new LaneSnapshot(
                [.. Items(c.RootElement, "lanes").Select(Lane)],
                [.. Items(s.RootElement, "mergeQueue").Select(Queued)],
                [.. Items(u.RootElement, "subjects").Select(Subject)]);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException or KeyNotFoundException)
        {
            return SindriErrors.LanesUnavailable.With("aw-meta printed JSON this build can't read: " + e.Message);
        }
    }

    private static List<JsonElement> Items(JsonElement root, string name)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("expected a JSON object");
        }

        return Find(root, name) is { ValueKind: JsonValueKind.Array } array ? [.. array.EnumerateArray()] : throw new KeyNotFoundException(name + "[] is missing");
    }

    private static JsonElement? Find(JsonElement element, string name)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }

        return null;
    }

    private static string Text(JsonElement element, string name) => Find(element, name) is { ValueKind: JsonValueKind.String } v ? v.GetString() ?? string.Empty : throw new KeyNotFoundException(name);

    private static string? OptionalText(JsonElement element, string name) => Find(element, name) is { ValueKind: JsonValueKind.String } v ? v.GetString() : null;

    private static long Number(JsonElement element, string name) => Find(element, name) is { ValueKind: JsonValueKind.Number } v ? v.GetInt64() : throw new KeyNotFoundException(name);

    private static QueuedPr Queued(JsonElement e) => new((int)Number(e, "pr"), Text(e, "from"), OptionalText(e, "github"));

    private static LaneContext Lane(JsonElement e) => new(Text(e, "lane"), Number(e, "tokens"), Number(e, "threshold"), Text(e, "verdict"));

    private static UsageSubject Subject(JsonElement e) => new(
        Text(e, "subject"),
        Text(e, "state"),
        OptionalText(e, "kind"),
        OptionalText(e, "resetsAt") is { } at ? DateTimeOffset.Parse(at, CultureInfo.InvariantCulture) : null);
}
