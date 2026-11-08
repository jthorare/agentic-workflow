using Aw.Kernel;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.OAuth;

namespace Aw.Sindri.Infrastructure.Connections;

/// <summary>IConnectionProbe over an authenticated HTTPS GET. The error names the status, never the token or the body.</summary>
/// <param name="http">The HTTP client.</param>
internal sealed class HttpConnectionProbe(HttpClient http) : IConnectionProbe
{
    private readonly HttpClient _http = http;

    public ValueTask<Result<Unit>> Probe(string url, TokenSet tokens, CancellationToken cancellationToken) => throw Pending();

    // A red stub: the ports are held so the tests build the class the way the green one is built.
    private NotSupportedException Pending()
    {
        _ = _http;
        return new NotSupportedException();
    }
}
