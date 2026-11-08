using System.Globalization;
using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.Polling;

namespace Aw.Sindri.Infrastructure.Connections.Polling;

/// <summary>
/// IEventPoller for Linear over a plain HttpClient: one GraphQL 'issues' query with each issue's history and comments. The delivery key uses
/// ids (history entry plus label or state id), so it is the same on every poll and holds no provider text. The token goes only to
/// api.linear.app: an OAuth token as Bearer, a personal API key bare (as LinearClient sends it). A redirect is refused (SND-CONN-012). A 400
/// that says RATELIMITED is a rate-limited result, not a rejected cursor. The cursor is the newest createdAt seen; a null cursor is a baseline.
/// Pages are followed (pageInfo.endCursor) up to the page cap; past it <see cref="PollResult.Gap"/> is true. Provider text in the events is untrusted.
/// </summary>
/// <param name="http">The HTTP client; it must not follow redirects on its own.</param>
/// <param name="secrets">The connection secrets.</param>
/// <param name="clock">The clock a baseline falls back to when the provider has no events.</param>
/// <param name="pageCap">The most pages one poll reads.</param>
internal sealed class LinearEventPoller(HttpClient http, IConnectionSecrets secrets, TimeProvider clock, int pageCap = PollLimits.DefaultPageCap) : IEventPoller
{
    private const string Host = "api.linear.app";

    private const string Fields = "identifier title url createdAt comments(first: 10) { nodes { id createdAt body user { name } } } history(first: 25) { nodes { id createdAt addedLabels { id name } fromState { id name } toState { id name } } }";

    private readonly PollerClient _client = new(http, secrets, Host, clock, bareKeys: true);

    public string Preset => "linear";

    public async ValueTask<Result<PollResult>> Poll(Connection connection, EventKind kind, string? cursor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (!PollCatalog.For(Preset).Contains(kind))
        {
            return SindriErrors.ConnectionProviderChanged.With($"{kind} is not a Linear event");
        }

        var after = PollJson.Time(cursor);
        var feed = new LinearFeed(kind, after, cursor);
        string? endCursor = null;
        for (var page = 1; ; page++)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"https://{Host}/graphql") { Content = new StringContent(Query(after, endCursor), Encoding.UTF8, "application/json") };
            var answer = await _client.Send(connection, request, cursor is not null, cancellationToken).ConfigureAwait(false);
            if (answer.IsError || answer.Value.Body is not { } body)
            {
                return answer.IsError ? answer.Error : new PollResult([], cursor, RateLimited: true, answer.Value.RetryAfter, Reason: answer.Value.Reason);
            }

            var added = feed.Add(body);
            if (added.IsError)
            {
                return added.Error;
            }

            if (after is null || feed.More is not { } more)
            {
                return Finish(feed, gap: false);
            }

            if (page >= pageCap)
            {
                return Finish(feed, gap: true);
            }

            endCursor = more;
        }
    }

    private static string Query(DateTimeOffset? after, string? endCursor)
    {
        var filter = after is { } floor ? $"filter: {{ updatedAt: {{ gt: \"{floor.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)}\" }} }}, " : string.Empty;
        var query = "query($after: String) { issues(after: $after, " + filter + "first: 50, orderBy: updatedAt) { nodes { " + Fields + " } pageInfo { hasNextPage endCursor } } }";
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("query", query);
            if (endCursor is not null)
            {
                writer.WriteStartObject("variables");
                writer.WriteString("after", endCursor);
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private Result<PollResult> Finish(LinearFeed feed, bool gap)
    {
        var events = feed.Events.DistinctBy(static e => e.DeliveryKey, StringComparer.Ordinal).OrderBy(static e => e.OccurredAt).ThenBy(static e => e.DeliveryKey, StringComparer.Ordinal).ToList();
        return new PollResult(events, feed.Newest.Text ?? PollCursorText.Now(clock.GetUtcNow()), RateLimited: false, RetryAfter: null, gap);
    }
}
