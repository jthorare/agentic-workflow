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

    private readonly PollerClient _client = new(http, secrets, Host, clock);

    public string Preset => "github";

    public async ValueTask<Result<PollResult>> Poll(Connection connection, EventKind kind, string? cursor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (!PollCatalog.For(Preset).Contains(kind))
        {
            return SindriErrors.ConnectionProviderChanged.With($"{kind} is not a GitHub event");
        }

        var parts = connection.Config.GetValueOrDefault("repo", string.Empty).Split('/');
        if (parts.Length != 2 || parts.Any(static p => p.Length == 0))
        {
            return SindriErrors.ConnectionProviderChanged.With($"{connection.Name} has no 'repo' setting like owner/name");
        }

        var first = $"https://{Host}/repos/{Uri.EscapeDataString(parts[0])}/{Uri.EscapeDataString(parts[1])}/events?per_page=100";
        return await ReadPages(connection, kind, cursor, first, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<PollResult>> ReadPages(Connection connection, EventKind kind, string? cursor, string first, CancellationToken cancellationToken)
    {
        var after = PollJson.Time(cursor);
        var feed = new GitHubFeed(kind, after is { } at ? at - Overlap : null);
        var next = first;
        for (var page = 1; ; page++)
        {
            var answer = await _client.Send(connection, new HttpRequestMessage(HttpMethod.Get, next), cursor is not null, cancellationToken).ConfigureAwait(false);
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
                return Finish(feed, cursor, after, gap: false);
            }

            if (page >= pageCap)
            {
                return Finish(feed, cursor, after, gap: true);
            }

            next = more;
        }
    }

    private Result<PollResult> Finish(GitHubFeed feed, string? cursor, DateTimeOffset? after, bool gap)
    {
        var newest = feed.Newest.Later(cursor);
        var events = after is null ? [] : feed.Events.DistinctBy(static e => e.DeliveryKey, StringComparer.Ordinal).OrderBy(static e => e.OccurredAt).ThenBy(static e => e.DeliveryKey, StringComparer.Ordinal).ToList();
        return new PollResult(events, newest.Text ?? PollCursorText.Now(clock.GetUtcNow()), RateLimited: false, RetryAfter: null, gap);
    }
}
