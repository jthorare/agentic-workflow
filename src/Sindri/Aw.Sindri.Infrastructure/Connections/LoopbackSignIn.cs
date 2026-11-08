using Aw.Kernel;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.OAuth;

namespace Aw.Sindri.Infrastructure.Connections;

/// <summary>
/// ISignInSession on 127.0.0.1: listens on a free port, opens the consent URL, takes ONE callback (state checked, code exchanged), and closes
/// the listener when the callback lands, on denial, on timeout and on cancel.
/// </summary>
/// <param name="oauth">Exchanges the code.</param>
/// <param name="browser">Opens the consent URL.</param>
/// <param name="timeout">How long to wait for the callback (5 minutes in production).</param>
/// <param name="randomBytes">The PKCE byte source.</param>
internal sealed class LoopbackSignIn(IOAuthClient oauth, IBrowserOpener browser, TimeSpan timeout, Func<int, byte[]> randomBytes) : ISignInSession
{
    private readonly IOAuthClient _oauth = oauth;
    private readonly IBrowserOpener _browser = browser;
    private readonly TimeSpan _timeout = timeout;
    private readonly Func<int, byte[]> _randomBytes = randomBytes;

    public ValueTask<Result<SignInStarted>> Start(ConnectionPreset preset, string clientId, IReadOnlyList<string> scopes, CancellationToken cancellationToken) => throw Pending();

    // A red stub: the ports are held so the tests build the class the way the green one is built.
    private NotSupportedException Pending()
    {
        _ = (_oauth, _browser, _timeout, _randomBytes);
        return new NotSupportedException();
    }
}
