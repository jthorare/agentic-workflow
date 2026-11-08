using Aw.Kernel;
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
    private readonly PollerClient _client = new(http, secrets, "slack.com");

    public string Preset => "slack";

    public ValueTask<Result<PollResult>> Poll(Connection connection, EventKind kind, string? cursor, CancellationToken cancellationToken)
    {
        _ = _client;
        throw new NotSupportedException("red stub: PR 11 part 1");
    }
}
