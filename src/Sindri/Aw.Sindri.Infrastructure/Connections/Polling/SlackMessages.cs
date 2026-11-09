using System.Globalization;
using System.Text.Json;
using Aw.Sindri.Core.Connections.Polling;

namespace Aw.Sindri.Infrastructure.Connections.Polling;

/// <summary>Decides whether a Slack message is the asked event and maps it. The text is provider text: untrusted.</summary>
internal static class SlackMessages
{
    internal static bool Matches(JsonElement message, SlackFilter filter)
    {
        var text = PollJson.Text(PollJson.Member(message, "text")) ?? string.Empty;
        return filter.Kind == EventKind.SlackMention
            ? filter.OwnerId.Length > 0 && text.Contains("<@" + filter.OwnerId + ">", StringComparison.Ordinal)
            : filter.Text.Length == 0 || text.Contains(filter.Text, StringComparison.OrdinalIgnoreCase);
    }

    internal static PolledEvent? Map(JsonElement message, SlackFilter filter, string ts)
    {
        if (!decimal.TryParse(ts, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
        {
            return null;
        }

        var fields = new Dictionary<string, string>(StringComparer.Ordinal) { ["channel"] = filter.Channel, ["ts"] = ts };
        PollJson.Put(fields, "user", PollJson.Text(PollJson.Member(message, "user")));
        PollJson.Put(fields, "text", PollJson.Text(PollJson.Member(message, "text")));
        return new PolledEvent($"slack:{filter.Channel}:{ts}", filter.Kind, DateTimeOffset.FromUnixTimeSeconds((long)decimal.Floor(value)), fields);
    }
}
