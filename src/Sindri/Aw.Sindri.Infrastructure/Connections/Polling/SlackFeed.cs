using System.Globalization;
using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections.Polling;

namespace Aw.Sindri.Infrastructure.Connections.Polling;

/// <summary>
/// The pages of one Slack conversations.history read so far (newest first): the matching events newer than the cursor, the newest ts of ANY
/// message, and the next page's cursor. Slack answers HTTP 200 with ok=false on errors: ratelimited sets <see cref="RateLimited"/>,
/// invalid_auth and token_revoked are SND-CONN-002, anything else SND-CONN-012.
/// </summary>
/// <param name="filter">What to look for.</param>
/// <param name="cursor">The ts the poll began with, or null for a baseline (which keeps no events).</param>
internal sealed class SlackFeed(SlackFilter filter, string? cursor)
{
    private readonly decimal? _after = Stamp(cursor);
    private readonly List<(decimal Ts, PolledEvent Event)> _found = [];
    private (string? Text, decimal? Value) _newest = (cursor, Stamp(cursor));

    internal bool RateLimited { get; private set; }

    internal string? NextPage { get; private set; }

    internal string? NewestTs => _newest.Text;

    internal IReadOnlyList<PolledEvent> Events => [.. _found.OrderBy(static f => f.Ts).Select(static f => f.Event)];

    internal Result<Unit> Add(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (PollJson.Member(root, "ok") is not { ValueKind: JsonValueKind.True })
            {
                return Failure(PollJson.Text(PollJson.Member(root, "error")));
            }

            if (PollJson.Member(root, "messages") is not { ValueKind: JsonValueKind.Array } messages)
            {
                return SindriErrors.ConnectionProviderChanged.With("the history answer has no message list");
            }

            NextPage = PollJson.Text(PollJson.Path(root, "response_metadata", "next_cursor")) is { Length: > 0 } next ? next : null;
            return Collect(messages);
        }
        catch (JsonException)
        {
            return SindriErrors.ConnectionProviderChanged.With("the history answer is not JSON");
        }
    }

    internal static decimal? Stamp(string? ts) => decimal.TryParse(ts, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value) ? value : null;

    private Result<Unit> Collect(JsonElement messages)
    {
        foreach (var message in messages.EnumerateArray())
        {
            if (PollJson.Text(PollJson.Member(message, "ts")) is not { } ts || Stamp(ts) is not { } value)
            {
                return SindriErrors.ConnectionProviderChanged.With("a message has no ts");
            }

            if (_newest.Value is not { } seen || value > seen)
            {
                _newest = (ts, value);
            }

            if (_after is not null && value > _after && SlackMessages.Matches(message, filter) && SlackMessages.Map(message, filter, ts) is { } polled)
            {
                _found.Add((value, polled));
            }
        }

        return Unit.Value;
    }

    private Result<Unit> Failure(string? slackError)
    {
        switch (slackError ?? "unknown")
        {
            case "ratelimited":
                RateLimited = true;
                return Unit.Value;
            case "invalid_auth" or "token_revoked" or "not_authed" or "account_inactive":
                return SindriErrors.ConnectionNeedsSignIn.With($"Slack answered {slackError}");
            default:
                return SindriErrors.ConnectionProviderChanged.With($"Slack answered {slackError ?? "ok=false"}");
        }
    }
}
