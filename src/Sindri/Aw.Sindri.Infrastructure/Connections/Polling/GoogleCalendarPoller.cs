using System.Globalization;
using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.Polling;

namespace Aw.Sindri.Infrastructure.Connections.Polling;

/// <summary>
/// IEventPoller for Google Calendar over a plain HttpClient: events of the calendar in Connection.Config["calendar"] (default 'primary') that start
/// within Config["withinMinutes"] (default 15) of now. The delivery key is 'google:&lt;eventId&gt;:&lt;start&gt;', so an event that moved fires again.
/// All-day events (no start time) are skipped. The token goes only to www.googleapis.com. Event text is untrusted. The cursor is not used: the
/// window is the filter, and the delivery key stops a second run.
/// </summary>
/// <param name="http">The HTTP client; it must not follow redirects on its own.</param>
/// <param name="secrets">The connection secrets.</param>
/// <param name="clock">The clock the window is measured from.</param>
internal sealed class GoogleCalendarPoller(HttpClient http, IConnectionSecrets secrets, TimeProvider clock) : IEventPoller
{
    private const string Host = "www.googleapis.com";
    private const int DefaultMinutes = 15;

    private readonly PollerClient _client = new(http, secrets, Host, clock);
    private readonly TimeProvider _clock = clock;

    public string Preset => "google";

    public async ValueTask<Result<PollResult>> Poll(Connection connection, EventKind kind, string? cursor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (kind != EventKind.GoogleCalendarEventStarting)
        {
            return SindriErrors.ConnectionProviderChanged.With($"{kind} is not a Google Calendar event");
        }

        var minutes = int.TryParse(connection.Config.GetValueOrDefault("withinMinutes"), NumberStyles.None, CultureInfo.InvariantCulture, out var asked) && asked > 0 ? asked : DefaultMinutes;
        var from = _clock.GetUtcNow();
        var to = from.AddMinutes(minutes);
        var calendar = Uri.EscapeDataString(connection.Config.GetValueOrDefault("calendar", "primary"));
        var url = $"https://{Host}/calendar/v3/calendars/{calendar}/events?singleEvents=true&orderBy=startTime&timeMin={Uri.EscapeDataString(Iso(from))}&timeMax={Uri.EscapeDataString(Iso(to))}";
        var answer = await _client.Send(connection, new HttpRequestMessage(HttpMethod.Get, url), cursorSent: false, cancellationToken).ConfigureAwait(false);
        if (answer.IsError)
        {
            return answer.Error;
        }

        return answer.Value.Body is { } body
            ? Read(body, from, to, cursor)
            : new PollResult([], cursor, RateLimited: true, answer.Value.RetryAfter, Reason: answer.Value.Reason);
    }

    private static string Iso(DateTimeOffset value) => value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static Result<PollResult> Read(string body, DateTimeOffset from, DateTimeOffset to, string? cursor)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            if (PollJson.Member(document.RootElement, "items") is not { ValueKind: JsonValueKind.Array } items)
            {
                return SindriErrors.ConnectionProviderChanged.With("the events answer has no item list");
            }

            var found = new List<PolledEvent>();
            foreach (var item in items.EnumerateArray())
            {
                var id = PollJson.Text(PollJson.Member(item, "id"));
                var rawStart = PollJson.Text(PollJson.Path(item, "start", "dateTime"));
                if (id is not null && rawStart is not null && PollJson.Time(rawStart) is { } start && start >= from && start <= to)
                {
                    found.Add(Map(item, id, rawStart, start));
                }
            }

            return new PollResult([.. found.OrderBy(static e => e.OccurredAt)], cursor, RateLimited: false, RetryAfter: null);
        }
        catch (JsonException)
        {
            return SindriErrors.ConnectionProviderChanged.With("the events answer is not JSON");
        }
    }

    private static PolledEvent Map(JsonElement item, string id, string rawStart, DateTimeOffset start)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal) { ["start"] = rawStart };
        PollJson.Put(fields, "title", PollJson.Text(PollJson.Member(item, "summary")));
        PollJson.Put(fields, "url", PollJson.Text(PollJson.Member(item, "htmlLink")));
        return new PolledEvent($"google:{id}:{rawStart}", EventKind.GoogleCalendarEventStarting, start, fields);
    }
}
