using Aw.Connectors.Core;
using Aw.Connectors.Core.Slack;

namespace Aw.Connectors.Features.PullSlack;

/// <summary>What one pull has read so far: its records, the cursor it moved, the replies reads made and a rate limit that stopped it.</summary>
internal sealed class PullRun(SlackCursor start)
{
    public List<ConnectorItem> Items { get; } = [];

    public SlackCursor Next { get; set; } = start;

    public int Replies { get; set; }

    public DateTimeOffset? Limited { get; set; }

    // why: the consumer reads an empty page as 'finished', so a pull goes on until it has records, then stops at the limit or the replies budget.
    public void SortFrom(int start)
    {
        var tail = Items.Skip(start).OrderBy(static i => i.CreatedAt).ThenBy(static i => i.Key, StringComparer.Ordinal).ToList();
        Items.RemoveRange(start, Items.Count - start);
        Items.AddRange(tail);
    }

    public bool Enough(int limit) => Items.Count > 0 && (Items.Count >= limit || Replies >= PullSlackService.RepliesPerPull);
}
