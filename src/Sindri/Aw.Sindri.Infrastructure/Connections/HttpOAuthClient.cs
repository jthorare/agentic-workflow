using Aw.Kernel;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.OAuth;

namespace Aw.Sindri.Infrastructure.Connections;

/// <summary>IOAuthClient over HTTPS form posts to the preset's token and revoke endpoints. A provider's error body never reaches an error message unscrubbed: the message names the status and the provider's error code only.</summary>
/// <param name="http">The HTTP client (tests point it at the fake OAuth server).</param>
/// <param name="time">The clock, for ExpiresAt.</param>
internal sealed class HttpOAuthClient(HttpClient http, TimeProvider time) : IOAuthClient
{
    private readonly HttpClient _http = http;
    private readonly TimeProvider _time = time;

    public ValueTask<Result<TokenSet>> ExchangeCode(CodeExchange exchange, CancellationToken cancellationToken) => throw Pending();

    public ValueTask<Result<TokenSet>> Refresh(ConnectionPreset preset, string client, string refreshToken, CancellationToken cancellationToken) => throw Pending();

    public ValueTask<Result<Unit>> Revoke(ConnectionPreset preset, string client, string token, CancellationToken cancellationToken) => throw Pending();

    // A red stub: the ports are held so the tests build the class the way the green one is built.
    private NotSupportedException Pending()
    {
        _ = (_http, _time);
        return new NotSupportedException();
    }
}
