using Aw.Connectors.Core.Slack;

namespace Aw.Connectors.Testing.Slack;

/// <summary>The messages a fake Slack holds, and how Slack reads them: history is the top-level messages newest first, a thread is its root then its replies oldest first.</summary>
internal sealed class SlackStore
{
    private readonly Dictionary<string, List<SlackMessage>> _channels = new(StringComparer.Ordinal);

    public void Add(string channelId, SlackMessage message)
    {
        if (!_channels.TryGetValue(channelId, out var list))
        {
            list = [];
            _channels[channelId] = list;
        }

        list.Add(message);
    }

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
    public List<SlackMessage> Thread(string channelId, string threadTs)
        => [.. All(channelId).Where(m => m.Ts == threadTs || m.ThreadTs == threadTs).OrderBy(static m => Pad(m.Ts), StringComparer.Ordinal)];

    private List<SlackMessage> All(string channelId) => _channels.TryGetValue(channelId, out var list) ? list : [];

    private static string Pad(string ts) => ts.PadLeft(20, '0');
}
