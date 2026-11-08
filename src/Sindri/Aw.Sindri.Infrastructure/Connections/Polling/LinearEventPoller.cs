using Aw.Kernel;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.Polling;

namespace Aw.Sindri.Infrastructure.Connections.Polling;

/// <summary>
/// IEventPoller for Linear over a plain HttpClient: the token comes from the connection's secret, goes only to api.linear.app, and a redirect is
/// refused (SND-CONN-012) instead of followed. Provider text in the events is untrusted.
/// </summary>
/// <param name="http">The HTTP client; it must not follow redirects on its own.</param>
/// <param name="secrets">The connection secrets.</param>
internal sealed class LinearEventPoller(HttpClient http, IConnectionSecrets secrets) : IEventPoller
{
    private readonly HttpClient _http = http;
    private readonly IConnectionSecrets _secrets = secrets;

    public string Preset => "linear";

    public ValueTask<Result<PollResult>> Poll(Connection connection, EventKind kind, string? cursor, CancellationToken cancellationToken)
    {
        _ = (_http, _secrets);
        throw new NotSupportedException("red stub: PR 11 part 1");
    }
}
