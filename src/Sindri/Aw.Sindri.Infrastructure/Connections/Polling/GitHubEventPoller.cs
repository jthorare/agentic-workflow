using System.Globalization;
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
/// </summary>
/// <param name="http">The HTTP client; it must not follow redirects on its own.</param>
/// <param name="secrets">The connection secrets.</param>
/// <param name="clock">The clock a baseline falls back to when the provider has no events.</param>
/// <param name="pageCap">The most pages one poll reads.</param>
internal sealed class GitHubEventPoller(HttpClient http, IConnectionSecrets secrets, TimeProvider clock, int pageCap = PollLimits.DefaultPageCap) : IEventPoller
{
    private const string Host = "api.github.com";

    private readonly PollerClient _client = new(http, secrets, Host, clock);

    internal int PageCap { get; } = pageCap;

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

        var url = $"https://{Host}/repos/{Uri.EscapeDataString(parts[0])}/{Uri.EscapeDataString(parts[1])}/events?per_page=100";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        var answer = await _client.Send(connection, request, cursor is not null, cancellationToken).ConfigureAwait(false);
        if (answer.IsError)
        {
            return answer.Error;
        }

        return answer.Value.Body is { } body
            ? Read(body, kind, cursor)
            : new PollResult([], cursor, RateLimited: true, answer.Value.RetryAfter);
    }

    private static Result<PollResult> Read(string body, EventKind kind, string? cursor)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return SindriErrors.ConnectionProviderChanged.With("the events answer is not a list");
            }

            var after = long.TryParse(cursor, NumberStyles.None, CultureInfo.InvariantCulture, out var saved) ? saved : 0L;
            var newest = after;
            var found = new List<(long Id, PolledEvent Event)>();
            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (!long.TryParse(PollJson.Text(PollJson.Member(item, "id")), NumberStyles.None, CultureInfo.InvariantCulture, out var id))
                {
                    return SindriErrors.ConnectionProviderChanged.With("an event has no numeric id");
                }

                newest = Math.Max(newest, id);
                if (id > after && GitHubEventMapper.KindOf(item) == kind && GitHubEventMapper.Map(item, kind) is { } polled)
                {
                    found.Add((id, polled));
                }
            }

            var next = newest == 0 ? cursor : newest.ToString(CultureInfo.InvariantCulture);
            return new PollResult([.. found.OrderBy(static f => f.Id).Select(static f => f.Event)], next, RateLimited: false, RetryAfter: null);
        }
        catch (JsonException)
        {
            return SindriErrors.ConnectionProviderChanged.With("the events answer is not JSON");
        }
    }
}
