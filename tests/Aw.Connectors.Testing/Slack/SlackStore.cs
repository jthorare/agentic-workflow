using Aw.Connectors.Core.Slack;

namespace Aw.Connectors.Testing.Slack;

/// <summary>
/// The messages a fake Slack holds, and how Slack reads them: history is the top-level messages newest first, a thread is its root then its replies oldest first.
/// Each message keeps the user id that wrote it (Slack's 'user' field).
/// </summary>
internal sealed class SlackStore
{
    /// <summary>The user id a message carries when the test gave none.</summary>
    public const string SampleUser = "U0000SAMPLE";

    private readonly Dictionary<string, List<SlackMessage>> _channels = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Channel, string Ts), string> _users = [];

    public void Add(string channelId, SlackMessage message, string? userId = null)
    {
        if (!_channels.TryGetValue(channelId, out var list))
        {
            list = [];
            _channels[channelId] = list;
        }

        list.Add(message);
        _users[(channelId, message.Ts)] = userId ?? SampleUser;
    }

    /// <summary>The user id that wrote the message with <paramref name="ts"/> in <paramref name="channelId"/>.</summary>
    public string UserOf(string channelId, string ts) => _users.TryGetValue((channelId, ts), out var user) ? user : SampleUser;

    /// <summary>Top-level messages with ts at or after <paramref name="oldest"/> and before <paramref name="latest"/> (when given), newest first; a root carries the reply count the store holds and its own ts as thread_ts.</summary>
    public List<SlackMessage> Top(string channelId, string? oldest, string? latest = null)
    {
        var all = All(channelId);
        return [.. all
            .Where(m => (m.ThreadTs is null || m.ThreadTs == m.Ts) && (oldest is null || string.CompareOrdinal(Pad(m.Ts), Pad(oldest)) >= 0) && (latest is null || string.CompareOrdinal(Pad(m.Ts), Pad(latest)) < 0))
            .OrderByDescending(static m => Pad(m.Ts), StringComparer.Ordinal)
            .Select(m =>
            {
                var replies = all.Where(r => r.ThreadTs == m.Ts && r.Ts != m.Ts).ToList();
                return replies.Count == 0 ? m : m with { ThreadTs = m.Ts, ReplyCount = replies.Count, LatestReply = replies.Select(static r => r.Ts).MaxBy(static t => Pad(t), StringComparer.Ordinal) };
            })];
    }

    /// <summary>The root and its replies, oldest first.</summary>
    public List<SlackMessage> Thread(string channelId, string threadTs) => Thread(channelId, threadTs, oldest: null, inclusive: false);

    /// <summary>
    /// The root, then its replies oldest first. With <paramref name="oldest"/>, only replies after it (at or after it when <paramref name="inclusive"/>) come back;
    /// the root always leads, as Slack's 'conversations.replies' does (guess until the probe's replies line reads a real answer with 'oldest').
    /// </summary>
    public List<SlackMessage> Thread(string channelId, string threadTs, string? oldest, bool inclusive)
    {
        var all = All(channelId);
        var root = all.Where(m => m.Ts == threadTs);   // why: as stored, so the reader contract sees the same root the in-memory reader returns
        var replies = all.Where(m => m.ThreadTs == threadTs && m.Ts != threadTs && After(m.Ts, oldest, inclusive)).OrderBy(static m => Pad(m.Ts), StringComparer.Ordinal);
        return [.. root, .. replies];
    }

    /// <summary>Every message in every channel, with its channel and author.</summary>
    public IEnumerable<(string Channel, SlackMessage Message, string User)> Everything()
        => _channels.SelectMany(c => c.Value.Select(m => (c.Key, m, UserOf(c.Key, m.Ts))));

    /// <summary>Slack's ts order: true when <paramref name="ts"/> is after <paramref name="oldest"/> (or equal and inclusive), or when there is no bound.</summary>
    public static bool After(string ts, string? oldest, bool inclusive)
    {
        if (oldest is null)
        {
            return true;
        }

        var order = string.CompareOrdinal(Pad(ts), Pad(oldest));
        return order > 0 || (inclusive && order == 0);
    }

    /// <summary>The ts as a fixed-width key that sorts in time order.</summary>
    public static string Pad(string ts) => ts.PadLeft(20, '0');

    private List<SlackMessage> All(string channelId) => _channels.TryGetValue(channelId, out var list) ? list : [];
}
