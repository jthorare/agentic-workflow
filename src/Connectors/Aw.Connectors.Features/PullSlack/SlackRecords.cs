using System.Globalization;
using System.Text.RegularExpressions;
using Aw.Connectors.Core;
using Aw.Connectors.Core.Slack;

namespace Aw.Connectors.Features.PullSlack;

/// <summary>
/// Maps Slack threads and day buckets onto <see cref="ConnectorItem"/>s (owned index spec §5.3). A thread (a root and its replies, root first) is one record keyed 'slack:&lt;channel&gt;/&lt;rootTs&gt;', the
/// key the earlier Slack feed made, so stored links and decisions stay valid. A channel's unthreaded messages are one record per UTC day keyed 'slack:&lt;channel&gt;/day/&lt;yyyy-MM-dd&gt;' (the spec fixes no
/// key for it). The text is each message as 'author: text' in order, cleaned by <see cref="TextCleaner"/> and scrubbed of Slack tokens; the audience of every record is 'slack:&lt;channel&gt;'.
/// </summary>
internal static partial class SlackRecords
{
    private const int MaxTitleLength = 80;

    /// <summary>The audience key of a channel's records: 'slack:&lt;channelId&gt;'. The Host derives the same from the record key's prefix.</summary>
    /// <param name="channelId">The channel id.</param>
    /// <returns>The key.</returns>
    public static string AudienceKey(string channelId) => "slack:" + channelId;

    /// <summary>True for a message that tells nothing the index wants: a join, a leave, a topic change, a pin.</summary>
    /// <param name="message">A message.</param>
    /// <returns>Whether to leave it out.</returns>
    public static bool IsNoise(SlackMessage message)
        => message.Subtype is { } subtype
            && (subtype.StartsWith("channel_", StringComparison.Ordinal) || subtype.StartsWith("group_", StringComparison.Ordinal)
                || string.Equals(subtype, "pinned_item", StringComparison.Ordinal) || string.Equals(subtype, "unpinned_item", StringComparison.Ordinal));

    /// <summary>True for a root that has replies, which is read as a thread.</summary>
    /// <param name="message">A top-level message.</param>
    /// <returns>Whether it starts a thread.</returns>
    public static bool IsThreadRoot(SlackMessage message) => message.ReplyCount > 0 && string.Equals(message.ThreadTs, message.Ts, StringComparison.Ordinal);

    /// <summary>True for a message that is not in a thread at all (a reply broadcast to the channel belongs to its thread and is not one).</summary>
    /// <param name="message">A top-level message.</param>
    /// <returns>Whether it goes in the day record.</returns>
    public static bool IsUnthreaded(SlackMessage message) => message.ThreadTs is null || (string.Equals(message.ThreadTs, message.Ts, StringComparison.Ordinal) && message.ReplyCount == 0);

    /// <summary>The UTC day a message was written, 'yyyy-MM-dd'; null when its ts does not parse.</summary>
    /// <param name="message">A message.</param>
    /// <returns>The day.</returns>
    public static string? DayOf(SlackMessage message) => SlackTs.TryParse(message.Ts, out var at) ? SlackTs.Day(at) : null;

    /// <summary>The record of a thread.</summary>
    /// <param name="channel">The channel.</param>
    /// <param name="workspace">The workspace name for the permalink.</param>
    /// <param name="messages">The root first, then its replies oldest first.</param>
    /// <returns>The record, or null when no message of it has any text.</returns>
    public static ConnectorItem? Thread(SlackChannelRef channel, string workspace, IReadOnlyList<SlackMessage> messages)
    {
        var root = messages[0];
        var lines = Lines(messages);
        if (lines.Count == 0 || !SlackTs.TryParse(root.Ts, out var created))
        {
            return null;
        }

        var title = $"#{channel.Name}: {Headline(lines[0].Text)}";
        return new ConnectorItem(
            $"slack:{channel.Channel}/{root.Ts}",
            "message",
            title,
            Text(title, lines),
            Scrub(root.Author),
            SlackPermalink.For(workspace, channel.Channel, root.Ts),
            "open",
            "open",
            created,
            Newest(messages, created),
            ClosedAt: null,
            VersionOf(Newest(messages, created)),
            [],
            Repo: null);
    }

    /// <summary>The record of a channel's unthreaded messages of one day.</summary>
    /// <param name="channel">The channel.</param>
    /// <param name="workspace">The workspace name for the permalink.</param>
    /// <param name="day">The UTC day, 'yyyy-MM-dd'.</param>
    /// <param name="messages">That day's messages, oldest first.</param>
    /// <returns>The record, or null when none of them has any text.</returns>
    public static ConnectorItem? Day(SlackChannelRef channel, string workspace, string day, IReadOnlyList<SlackMessage> messages)
    {
        var said = messages.Where(m => Lines([m]).Count > 0).ToList();
        if (said.Count == 0 || !SlackTs.TryParse(said[0].Ts, out var created))
        {
            return null;
        }

        var title = $"#{channel.Name} {day}";
        var updated = Newest(said, created);
        return new ConnectorItem(
            $"slack:{channel.Channel}/day/{day}",
            "message",
            title,
            Text(title, Lines(said)),
            Author: null,
            SlackPermalink.For(workspace, channel.Channel, said[0].Ts),
            "open",
            "open",
            created,
            updated,
            ClosedAt: null,
            VersionOf(updated),
            [],
            Repo: null);
    }

    /// <summary>The round-trip form of an instant, the store's version of a record.</summary>
    /// <param name="at">The record's change time.</param>
    /// <returns>The version.</returns>
    public static string VersionOf(DateTimeOffset at) => at.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

    /// <summary>The change time of a thread or a day: its newest message.</summary>
    /// <param name="messages">The messages.</param>
    /// <param name="fallback">The time to use when none has a ts that parses.</param>
    /// <returns>The newest time.</returns>
    public static DateTimeOffset Newest(IReadOnlyList<SlackMessage> messages, DateTimeOffset fallback)
        => messages.Select(static m => SlackTs.TryParse(m.Ts, out var at) ? at : (DateTimeOffset?)null).OfType<DateTimeOffset>().DefaultIfEmpty(fallback).Max();

    private static List<(string? Author, string Text)> Lines(IReadOnlyList<SlackMessage> messages)
        => [.. messages.Select(static m => (Author: Scrub(m.Author), Text: Scrub(TextCleaner.Clean(m.Text)) ?? string.Empty)).Where(static l => l.Text.Length > 0)];

    private static string Text(string title, List<(string? Author, string Text)> lines)
        => title + "\n\n" + string.Join('\n', lines.Select(static l => $"{l.Author ?? "unknown"}: {l.Text}"));

    private static string Headline(string text)
    {
        var first = string.Join(' ', text.Split('\n', 2)[0].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return first.Length > MaxTitleLength ? first[..MaxTitleLength] + "…" : first;
    }

    private static string? Scrub(string? text) => text is null ? null : Tokens.Replace(text, "[redacted]");

    // Slack bot, user, app, refresh and legacy tokens ('xoxb-…', 'xoxe.xoxp-…', 'xapp-…'). Stored text is scrubbed again on ingest (Aw.Redaction); this keeps the obvious ones out of the record at the source.
    [GeneratedRegex(@"(?:xoxe\.)?xox[a-z]-[A-Za-z0-9-]{8,}|xapp-[A-Za-z0-9-]{8,}", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Tokens { get; }
}
