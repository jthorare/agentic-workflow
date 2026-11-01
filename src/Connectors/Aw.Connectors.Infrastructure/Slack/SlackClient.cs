using System.Globalization;
using Aw.Connectors.Core;
using Aw.Connectors.Core.Slack;
using Aw.Kernel;

namespace Aw.Connectors.Infrastructure.Slack;

/// <summary>
/// Reads the listed channels from the Slack Web API (owned index spec §5.3, §10): GET conversations.history and conversations.replies with the bot token as a bearer, a page at a time by
/// response_metadata.next_cursor. A channel that is not in the allowlist (or is a direct message) is refused before any request (SND-INDEX-013). Slack's HTTP 200 with ok:false becomes a typed
/// error; a 429 stops the read and comes back as a value with the reset time (never a sleep). Only https on slack.com is spoken, a redirect is refused and the token goes only in the
/// Authorization header and in no error.
/// </summary>
internal sealed partial class SlackClient : ISlackReader
{
    private const string Root = "https://slack.com/api";
    private const int PageSize = 200;
    private const int MaxHistoryPages = 500;
    private const int MaxReplyPages = 50;
    private readonly HttpClient _http;
    private readonly SlackClientSettings _settings;
    private readonly HashSet<string> _allowed;
    private readonly TimeProvider _clock;

    /// <summary>Creates the client.</summary>
    /// <param name="http">The client; its handler must not follow redirects (see <see cref="SlackHttp.CreateHandler"/>).</param>
    /// <param name="settings">The allowlist and the token.</param>
    /// <param name="clock">The clock a rate-limit wait is counted from.</param>
    public SlackClient(HttpClient http, SlackClientSettings settings, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(clock);
        _http = http;
        _settings = settings;
        _allowed = new HashSet<string>(settings.Channels, StringComparer.Ordinal);
        _clock = clock;
    }

    /// <inheritdoc />
    public async ValueTask<Result<SlackMessagePage>> History(string channel, string? oldest, string? latest, CancellationToken cancellationToken)
    {
        if (Prepare(channel) is { } refused)
        {
            return refused;
        }

        var window = Window(oldest, latest);
        if (window.IsError)
        {
            return window.Error;
        }

        return await ReadPages(cursor => Url($"conversations.history?channel={channel}&limit={PageSize.ToString(CultureInfo.InvariantCulture)}{window.Value}", cursor), MaxHistoryPages, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<Result<SlackMessagePage>> HistoryPage(string channel, string? oldest, string? latest, int limit, CancellationToken cancellationToken)
    {
        if (Prepare(channel) is { } refused)
        {
            return refused;
        }

        var window = Window(oldest, latest);
        if (window.IsError)
        {
            return window.Error;
        }

        var size = Math.Clamp(limit, 1, PageSize).ToString(CultureInfo.InvariantCulture);
        var step = await Fetch(Url($"conversations.history?channel={channel}&limit={size}{window.Value}", cursor: null), cancellationToken).ConfigureAwait(false);
        if (step.IsError)
        {
            return step.Error;
        }

        if (step.Value.RateLimitedUntil is { } until)
        {
            return new SlackMessagePage([], until);
        }

        var read = step.Value.Body is { } body ? SlackWire.ReadPage(body) : null;
        if (read is null)
        {
            return ConnectorsErrors.BadReply.With("A message of Slack's reply lacked a field the connector reads.");
        }

        var hasMore = read.Value.Next is not null || (step.Value.Body is { } root && root.TryGetProperty("has_more", out var flag) && flag.ValueKind == System.Text.Json.JsonValueKind.True);
        return new SlackMessagePage(read.Value.Messages, RateLimitedUntil: null, hasMore);
    }

    /// <inheritdoc />
    public async ValueTask<Result<SlackMessagePage>> Replies(string channel, string threadTs, CancellationToken cancellationToken)
    {
        if (Prepare(channel) is { } refused)
        {
            return refused;
        }

        if (!SlackTs.TryParse(threadTs, out _))
        {
            return ConnectorsErrors.Invalid.With("The thread must be named by a Slack ts.");
        }

        return await ReadPages(cursor => Url($"conversations.replies?channel={channel}&ts={threadTs}&limit={PageSize.ToString(CultureInfo.InvariantCulture)}", cursor), MaxReplyPages, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<Result<DateTimeOffset?>> Probe(string channel, CancellationToken cancellationToken)
    {
        if (Prepare(channel) is { } refused)
        {
            return refused;
        }

        var step = await Fetch(Url($"conversations.history?channel={channel}&limit=1", cursor: null), cancellationToken).ConfigureAwait(false);
        return step.IsError ? step.Error : Results.Success(step.Value.RateLimitedUntil);
    }

    private static Result<string> Window(string? oldest, string? latest)
    {
        if (oldest is not null && !SlackTs.TryParse(oldest, out _))
        {
            return ConnectorsErrors.Invalid.With("oldest must be a Slack ts.");
        }

        if (latest is not null && !SlackTs.TryParse(latest, out _))
        {
            return ConnectorsErrors.Invalid.With("latest must be a Slack ts.");
        }

        return (oldest is null ? string.Empty : $"&inclusive=true&oldest={oldest}") + (latest is null ? string.Empty : $"&latest={latest}");
    }

    private static Uri Url(string path, string? cursor)
        => new($"{Root}/{path}{(cursor is null ? string.Empty : "&cursor=" + Uri.EscapeDataString(cursor))}");

    /// <summary>The refusal for a channel that is not allowed (no request is sent), or for a token that is missing or malformed; null when the call may go on.</summary>
    private Error? Prepare(string channel)
    {
        if (!SlackChannelIds.IsReadable(channel) || !_allowed.Contains(channel))
        {
            return ConnectorsErrors.ChannelNotAllowed.With("That channel is not in context.sources.slack.channels, so it is not read.");
        }

        if (string.IsNullOrEmpty(_settings.Token))
        {
            return ConnectorsErrors.Unauthorized.With("There is no Slack token; run 'sindri index connect slack'.");
        }

        return _settings.Token.Any(static c => char.IsWhiteSpace(c) || char.IsControl(c))
            ? ConnectorsErrors.Invalid.With("The Slack token has whitespace or control characters; point the profile at the token itself.")
            : null;
    }

    private async ValueTask<Result<SlackMessagePage>> ReadPages(Func<string?, Uri> urlFor, int maxPages, CancellationToken cancellationToken)
    {
        List<SlackMessage> items = [];
        string? cursor = null;
        for (var page = 0; page < maxPages; page++)
        {
            var step = await Fetch(urlFor(cursor), cancellationToken).ConfigureAwait(false);
            if (step.IsError)
            {
                return step.Error;
            }

            if (step.Value.RateLimitedUntil is { } until)
            {
                return new SlackMessagePage(items, until);
            }

            var read = step.Value.Body is { } body ? SlackWire.ReadPage(body) : null;
            if (read is null)
            {
                return ConnectorsErrors.BadReply.With("A message of Slack's reply lacked a field the connector reads.");
            }

            items.AddRange(read.Value.Messages);
            if (read.Value.Next is not { } next)
            {
                return new SlackMessagePage(items, RateLimitedUntil: null);
            }

            cursor = next;
        }

        return ConnectorsErrors.BadReply.With(string.Create(CultureInfo.InvariantCulture, $"The channel has more than {maxPages} pages of messages; narrow context.index.windowDays."));
    }
}
