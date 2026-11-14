using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.Polling;

namespace Aw.Sindri.Infrastructure.Connections.Polling;

/// <summary>
/// IEventPoller for GitHub over a plain HttpClient: the repo's events feed (Connection.Config["repo"] = 'owner/name'). The token comes
/// from the connection's secret, goes only to api.github.com, and a redirect is refused (SND-CONN-012) instead of followed. Provider text
/// in the events is untrusted.
/// The cursor is the newest created_at seen. The feed can lag by 30 s to 6 h, so an id or time order is not safe: each poll re-reads
/// everything created after cursor minus 6 h (<see cref="Overlap"/>) and returns it, repeats included. A repeat is harmless: the delivery
/// key is the run's occurrence id and the caller's duplicate-id refusal drops it. The feed is newest first, so the poller follows the Link
/// header up to the page cap and returns the events oldest first; past the cap <see cref="PollResult.Gap"/> is true and the cursor still
/// moves to the newest event.
/// </summary>
/// <param name="http">The HTTP client; it must not follow redirects on its own.</param>
/// <param name="secrets">The connection secrets.</param>
/// <param name="clock">The clock a baseline falls back to when the provider has no events.</param>
/// <param name="pageCap">The most pages one poll reads.</param>
internal sealed class GitHubEventPoller(HttpClient http, IConnectionSecrets secrets, TimeProvider clock, int pageCap = PollLimits.DefaultPageCap) : IEventPoller
{
    internal static readonly TimeSpan Overlap = TimeSpan.FromHours(6);

    private const string Host = "api.github.com";
    private const int MaxEnriched = 20;   // GET /pulls/{n} calls per poll

    private readonly PollerClient _client = new(http, secrets, Host, clock);
    private readonly GitHubPullStates _states = new(new PollerClient(http, secrets, Host, clock));

    public string Preset => "github";

    public async ValueTask<Result<PollResult>> Poll(Connection connection, EventKind kind, string? cursor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (!PollCatalog.For(Preset).Contains(kind) && !GitHubEventMapper.Knows(kind))   // why: the mapper's kinds wait on engine's catalog PR (#3201); validate still refuses a kind the catalog lacks
        {
            return SindriErrors.ConnectionProviderChanged.With($"{kind} is not a GitHub event");
        }

        var parts = connection.Config.GetValueOrDefault("repo", string.Empty).Split('/');
        if (parts.Length != 2 || parts.Any(static p => p.Length == 0))
        {
            return SindriErrors.ConnectionProviderChanged.With($"{connection.Name} has no 'repo' setting like owner/name");
        }

        return GitHubEventMapper.FromPullState(kind)
            ? await _states.Poll(connection, parts, kind, cursor, cancellationToken).ConfigureAwait(false)
            : await ReadPages(new PollTarget(connection, parts[0], parts[1]), kind, cursor, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<PollResult>> ReadPages(PollTarget target, EventKind kind, string? cursor, CancellationToken cancellationToken)
    {
        var after = PollJson.Time(cursor);
        var feed = new GitHubFeed(kind, after is { } at ? at - Overlap : null);
        var next = target.Url("events?per_page=100");
        for (var page = 1; ; page++)
        {
            var answer = await _client.Send(target.Connection, new HttpRequestMessage(HttpMethod.Get, next), cursorSent: false, cancellationToken).ConfigureAwait(false);
            if (answer.IsError || answer.Value.Body is not { } body)
            {
                return answer.IsError ? answer.Error : new PollResult([], cursor, RateLimited: true, answer.Value.RetryAfter, Reason: answer.Value.Reason);
            }

            var added = feed.Add(body);
            if (added.IsError)
            {
                return added.Error;
            }

            if (feed.ReachedFloor || answer.Value.NextUrl is not { } more)
            {
                return await Finish(target, feed, cursor, gap: false, cancellationToken).ConfigureAwait(false);
            }

            if (page >= pageCap)
            {
                return await Finish(target, feed, cursor, gap: true, cancellationToken).ConfigureAwait(false);
            }

            next = more;
        }
    }

    private async ValueTask<Result<PollResult>> Finish(PollTarget target, GitHubFeed feed, string? cursor, bool gap, CancellationToken cancellationToken)
    {
        var newest = feed.Newest.Later(cursor);
        var events = PollJson.Time(cursor) is null ? [] : feed.Events.DistinctBy(static e => e.DeliveryKey, StringComparer.Ordinal).OrderBy(static e => e.OccurredAt).ThenBy(static e => e.DeliveryKey, StringComparer.Ordinal).ToList();
        // Only the newest MaxEnriched get a GET /pulls/{n}: in a burst those are the ones a person acts on next; older ones go out as the feed had them.
        for (var i = events.Count - 1; i >= 0 && i >= events.Count - MaxEnriched; i--)
        {
            events[i] = await Enrich(target, events[i], cancellationToken).ConfigureAwait(false);
        }

        return new PollResult(events, newest.Text ?? PollCursorText.Now(clock.GetUtcNow()), RateLimited: false, RetryAfter: null, gap);
    }

    // The trimmed pull request in the feed holds no title, author or draft flag: one GET /pulls/{n} fills them. A failed or odd answer leaves the event as the feed had it.
    private async ValueTask<PolledEvent> Enrich(PollTarget target, PolledEvent polled, CancellationToken cancellationToken)
    {
        if (!GitHubEventMapper.NeedsDetail(polled) || !polled.Fields.TryGetValue("number", out var number) || number.Length == 0)
        {
            return polled;
        }

        var url = target.Url($"pulls/{Uri.EscapeDataString(number)}");
        var answer = await _client.Send(target.Connection, new HttpRequestMessage(HttpMethod.Get, url), cursorSent: false, cancellationToken).ConfigureAwait(false);
        if (answer.IsError || answer.Value.Body is not { } body)
        {
            return polled;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.ValueKind == JsonValueKind.Object ? GitHubEventMapper.Enriched(polled, document.RootElement) : polled;
        }
        catch (JsonException)
        {
            return polled;
        }
    }

    // The connection and its 'owner/name' repo, passed together through one poll.
    private readonly record struct PollTarget(Connection Connection, string Owner, string Name)
    {
        public string Url(string rest) => $"https://{Host}/repos/{Uri.EscapeDataString(Owner)}/{Uri.EscapeDataString(Name)}/{rest}";
    }
}
