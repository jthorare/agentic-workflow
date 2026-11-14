using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.Polling;

namespace Aw.Sindri.Infrastructure.Connections.Polling;

/// <summary>
/// IEventPoller for Slack over a plain HttpClient: conversations.history of the channel in Connection.Config["channel"], with the optional
/// "filter" (a word the text must contain) and "userId" (the owner, for mentions). The cursor is the newest message ts; a null cursor is a
/// baseline (no events). The history is newest first, so the poller follows response_metadata.next_cursor up to the page cap and returns the
/// events oldest first; past the cap <see cref="PollResult.Gap"/> is true. Slack answers HTTP 200 with ok=false on errors: ratelimited is a
/// rate-limited result, invalid_auth and token_revoked are SND-CONN-002, anything else SND-CONN-012. The token goes only to slack.com.
/// Message text is untrusted.
/// </summary>
/// <param name="http">The HTTP client; it must not follow redirects on its own.</param>
/// <param name="secrets">The connection secrets.</param>
/// <param name="clock">The clock a baseline falls back to when the channel has no messages.</param>
/// <param name="pageCap">The most pages one poll reads.</param>
internal sealed class SlackEventPoller(HttpClient http, IConnectionSecrets secrets, TimeProvider clock, int pageCap = PollLimits.DefaultPageCap) : IEventPoller
{
    private const string Host = "slack.com";

    private readonly PollerClient _client = new(http, secrets, Host, clock);

    public string Preset => "slack";

    public async ValueTask<Result<PollResult>> Poll(Connection connection, EventKind kind, string? cursor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        // slack:messages has no preset on purpose, so generic tools can't read it (#3279).
        if (connection.Preset is null)
        {
            return SindriErrors.ConnectionPresetMissing.With($"{connection.Name} has no preset");
        }

        if (!PollCatalog.For(Preset).Contains(kind))
        {
            return SindriErrors.ConnectionProviderChanged.With($"{kind} is not a Slack event");
        }

        var channel = connection.Config.GetValueOrDefault("channel", string.Empty);
        if (channel.Length == 0)
        {
            return SindriErrors.ConnectionProviderChanged.With($"{connection.Name} has no 'channel' setting");
        }

        var filter = new SlackFilter(kind, channel, connection.Config.GetValueOrDefault("filter", string.Empty), connection.Config.GetValueOrDefault("userId", string.Empty));
        var url = $"https://{Host}/api/conversations.history?channel={Uri.EscapeDataString(channel)}&limit=100" + (cursor is null ? string.Empty : "&oldest=" + Uri.EscapeDataString(cursor));
        return await ReadPages(connection, new SlackFeed(filter, cursor), url, cursor, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<PollResult>> ReadPages(Connection connection, SlackFeed feed, string url, string? cursor, CancellationToken cancellationToken)
    {
        var page = string.Empty;
        for (var number = 1; ; number++)
        {
            var answer = await _client.Send(connection, new HttpRequestMessage(HttpMethod.Get, url + page), cursorSent: false, cancellationToken).ConfigureAwait(false);
            if (answer.IsError || answer.Value.Body is not { } body)
            {
                return answer.IsError ? answer.Error : new PollResult([], cursor, RateLimited: true, answer.Value.RetryAfter, Reason: answer.Value.Reason);
            }

            var added = feed.Add(body);
            if (added.IsError)
            {
                return added.Error;
            }

            if (feed.RateLimited)
            {
                return new PollResult([], cursor, RateLimited: true, RetryAfter: null, Reason: "the provider is rate limiting this connection");
            }

            if (cursor is null || feed.NextPage is not { } next)
            {
                return Finish(feed, gap: false);
            }

            if (number >= pageCap)
            {
                return Finish(feed, gap: true);
            }

            page = "&cursor=" + Uri.EscapeDataString(next);
        }
    }

    private PollResult Finish(SlackFeed feed, bool gap)
    {
        var next = feed.NewestTs ?? (clock.GetUtcNow().ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture) + ".000000");
        return new PollResult(feed.Events, next, RateLimited: false, RetryAfter: null, gap);
    }
}
