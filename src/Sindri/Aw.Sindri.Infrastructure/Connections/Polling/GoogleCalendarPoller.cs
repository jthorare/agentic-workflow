using Aw.Kernel;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.Polling;

namespace Aw.Sindri.Infrastructure.Connections.Polling;

/// <summary>
/// IEventPoller for Google Calendar over a plain HttpClient: events of the calendar in Connection.Config["calendar"] (default 'primary') that start
/// within Config["withinMinutes"] (default 15) of now. The delivery key is 'google:&lt;eventId&gt;:&lt;start&gt;', so an event that moved fires again.
/// All-day events (no start time) are skipped. The token goes only to www.googleapis.com. Event text is untrusted.
/// </summary>
/// <param name="http">The HTTP client; it must not follow redirects on its own.</param>
/// <param name="secrets">The connection secrets.</param>
/// <param name="clock">The clock the window is measured from.</param>
internal sealed class GoogleCalendarPoller(HttpClient http, IConnectionSecrets secrets, TimeProvider clock) : IEventPoller
{
    private readonly PollerClient _client = new(http, secrets, "www.googleapis.com");
    private readonly TimeProvider _clock = clock;

    public string Preset => "google";

    public ValueTask<Result<PollResult>> Poll(Connection connection, EventKind kind, string? cursor, CancellationToken cancellationToken)
    {
        _ = (_client, _clock);
        throw new NotSupportedException("red stub: PR 11 part 1");
    }
}
