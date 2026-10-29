using Aw.Connectors.Core;
using Aw.Connectors.Core.Slack;

namespace Aw.Connectors.Features.PullSlack;

/// <summary>What a Slack pull read.</summary>
/// <param name="Items">The records read, kept even when a rate limit or the limit stopped the pull partway through a channel. Empty only when nothing is left to read (or a rate limit stopped the pull before any record).</param>
/// <param name="Next">Where the next pull starts: channels read to their end have moved their watermark, a channel stopped partway keeps its place inside it; unchanged when nothing was read.</param>
/// <param name="RateLimitedUntil">Set when Slack's rate limit stopped the run; the next pull should wait until then.</param>
internal sealed record PullSlackResponse(IReadOnlyList<ConnectorItem> Items, SlackCursor Next, DateTimeOffset? RateLimitedUntil);
