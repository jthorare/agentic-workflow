using System.Globalization;
using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.Polling;

namespace Aw.Sindri.Infrastructure.Connections.Polling;

/// <summary>
/// IEventPoller for Slack over a plain HttpClient: conversations.history of the channel in Connection.Config["channel"], with the optional
/// "filter" (a word the text must contain) and "userId" (the owner, for mentions). The cursor is the newest message ts. Slack answers HTTP 200
/// with ok=false on errors: ratelimited is a rate-limited result, invalid_auth and token_revoked are SND-CONN-002, anything else SND-CONN-012.
/// The token goes only to slack.com. Message text is untrusted.
/// </summary>
/// <param name="http">The HTTP client; it must not follow redirects on its own.</param>
/// <param name="secrets">The connection secrets.</param>
internal sealed class SlackEventPoller(HttpClient http, IConnectionSecrets secrets) : IEventPoller
{
    private const string Host = "slack.com";

    private readonly PollerClient _client = new(http, secrets, Host);

    public string Preset => "slack";

    public async ValueTask<Result<PollResult>> Poll(Connection connection, EventKind kind, string? cursor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (!PollCatalog.For(Preset).Contains(kind))
        {
            return SindriErrors.ConnectionProviderChanged.With($"{kind} is not a Slack event");
        }

        var channel = connection.Config.GetValueOrDefault("channel", string.Empty);
        if (channel.Length == 0)
        {
            return SindriErrors.ConnectionProviderChanged.With($"{connection.Name} has no 'channel' setting");
        }

        var url = $"https://{Host}/api/conversations.history?channel={Uri.EscapeDataString(channel)}&limit=100" + (cursor is null ? string.Empty : "&oldest=" + Uri.EscapeDataString(cursor));
        var answer = await _client.Send(connection, new HttpRequestMessage(HttpMethod.Get, url), cursor is not null, cancellationToken).ConfigureAwait(false);
        if (answer.IsError)
        {
            return answer.Error;
        }

        return answer.Value.Body is { } body
            ? Read(body, new SlackFilter(kind, channel, connection.Config.GetValueOrDefault("filter", string.Empty), connection.Config.GetValueOrDefault("userId", string.Empty)), cursor)
            : new PollResult([], cursor, RateLimited: true, answer.Value.RetryAfter);
    }

    private static Result<PollResult> Read(string body, SlackFilter filter, string? cursor)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (PollJson.Member(root, "ok") is not { ValueKind: JsonValueKind.True })
            {
                return Failure(PollJson.Text(PollJson.Member(root, "error")), cursor);
            }

            if (PollJson.Member(root, "messages") is not { ValueKind: JsonValueKind.Array } messages)
            {
                return SindriErrors.ConnectionProviderChanged.With("the history answer has no message list");
            }

            return Collect(messages, filter, cursor);
        }
        catch (JsonException)
        {
            return SindriErrors.ConnectionProviderChanged.With("the history answer is not JSON");
        }
    }

    private static Result<PollResult> Collect(JsonElement messages, SlackFilter filter, string? cursor)
    {
        var after = Stamp(cursor);
        var newest = (Text: cursor, Value: after);
        var found = new List<(decimal Ts, PolledEvent Event)>();
        foreach (var message in messages.EnumerateArray())
        {
            if (PollJson.Text(PollJson.Member(message, "ts")) is not { } ts || Stamp(ts) is not { } value)
            {
                return SindriErrors.ConnectionProviderChanged.With("a message has no ts");
            }

            if (newest.Value is not { } seen || value > seen)
            {
                newest = (ts, value);
            }

            if ((after is null || value > after) && SlackMessages.Matches(message, filter) && SlackMessages.Map(message, filter, ts) is { } polled)
            {
                found.Add((value, polled));
            }
        }

        return new PollResult([.. found.OrderBy(static f => f.Ts).Select(static f => f.Event)], newest.Text, RateLimited: false, RetryAfter: null);
    }

    private static Result<PollResult> Failure(string? slackError, string? cursor) => (slackError ?? "unknown") switch
    {
        "ratelimited" => new PollResult([], cursor, RateLimited: true, RetryAfter: null),
        "invalid_auth" or "token_revoked" or "not_authed" or "account_inactive" => SindriErrors.ConnectionNeedsSignIn.With($"Slack answered {slackError}"),
        _ => SindriErrors.ConnectionProviderChanged.With($"Slack answered {slackError ?? "ok=false"}"),
    };

    private static decimal? Stamp(string? ts) => decimal.TryParse(ts, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value) ? value : null;
}
