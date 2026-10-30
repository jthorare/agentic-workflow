using Aw.Kernel;

namespace Aw.Connectors.Core.Slack;

/// <summary>Reads the messages of the channels the profile lists. Every call refuses a channel that is not in the allowlist, before any request (SND-INDEX-013).</summary>
public interface ISlackReader
{
    /// <summary>Reads a channel's top-level messages (conversations.history), newest first, every page from <paramref name="oldest"/> (inclusive) to <paramref name="latest"/> (exclusive), or to now.</summary>
    /// <param name="channel">The channel id.</param>
    /// <param name="oldest">The ts to start from (inclusive), or null for the whole channel.</param>
    /// <param name="latest">The ts to stop before (exclusive; conversations.history's 'latest'), or null for no bound.</param>
    /// <param name="cancellationToken">The caller's cancellation.</param>
    /// <returns>The messages (a rate limit is a value in the page, not an error), or an error from the Connectors catalogue.</returns>
    public ValueTask<Result<SlackMessagePage>> History(string channel, string? oldest, string? latest, CancellationToken cancellationToken);

    /// <summary>Reads ONE page of a channel's top-level messages (one conversations.history request), newest first: at most <paramref name="limit"/> messages from <paramref name="oldest"/> (inclusive) up to <paramref name="latest"/> (exclusive). The caller pages by passing the oldest ts it got as the next <paramref name="latest"/>, never by Slack's next_cursor (which can expire).</summary>
    /// <param name="channel">The channel id.</param>
    /// <param name="oldest">The ts to start from (inclusive), or null for the whole channel.</param>
    /// <param name="latest">The ts to stop before (exclusive), or null for no bound.</param>
    /// <param name="limit">The most messages to return.</param>
    /// <param name="cancellationToken">The caller's cancellation.</param>
    /// <returns>The page, with <see cref="SlackMessagePage.HasMore"/> set when older messages remain (a rate limit is a value in the page, not an error), or an error from the Connectors catalogue.</returns>
    public async ValueTask<Result<SlackMessagePage>> HistoryPage(string channel, string? oldest, string? latest, int limit, CancellationToken cancellationToken)
    {
        var whole = await History(channel, oldest, latest, cancellationToken).ConfigureAwait(false);
        if (whole.IsError || whole.Value.RateLimitedUntil is not null)
        {
            return whole;
        }

        // why: a reader that only reads whole falls back to cutting its answer; the real client and the fake override this with one request.
        var items = whole.Value.Items;
        return new SlackMessagePage([.. items.Take(Math.Max(1, limit))], RateLimitedUntil: null, HasMore: items.Count > Math.Max(1, limit));
    }

    /// <summary>Reads a thread (conversations.replies): the root, then its replies, oldest first.</summary>
    /// <param name="channel">The channel id.</param>
    /// <param name="threadTs">The root's ts.</param>
    /// <param name="cancellationToken">The caller's cancellation.</param>
    /// <returns>The messages (a rate limit is a value in the page, not an error), or an error from the Connectors catalogue.</returns>
    public ValueTask<Result<SlackMessagePage>> Replies(string channel, string threadTs, CancellationToken cancellationToken);

    /// <summary>Checks the token and the channel with one cheap call (conversations.history, one message).</summary>
    /// <param name="channel">The channel id.</param>
    /// <param name="cancellationToken">The caller's cancellation.</param>
    /// <returns>Success (a rate limit is a value: its reset time), or the error that says what is wrong.</returns>
    public ValueTask<Result<DateTimeOffset?>> Probe(string channel, CancellationToken cancellationToken);
}
