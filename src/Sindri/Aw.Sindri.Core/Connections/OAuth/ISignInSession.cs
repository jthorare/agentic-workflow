using Aw.Kernel;

namespace Aw.Sindri.Core.Connections.OAuth;

/// <summary>
/// Starts one browser sign-in: a listener on 127.0.0.1:&lt;free port&gt; takes ONE callback, checks its state, exchanges the code,
/// and closes (also on timeout or denial). It writes nothing: the caller stores the tokens.
/// </summary>
public interface ISignInSession
{
    /// <summary>Starts a sign-in and opens the browser when it can.</summary>
    /// <param name="preset">The provider.</param>
    /// <param name="client">The OAuth client id.</param>
    /// <param name="scopes">The scopes; empty takes the preset's defaults.</param>
    /// <param name="clientSecret">The OAuth client secret for the token exchange, or null for a public client.</param>
    /// <param name="cancellationToken">Cancels the sign-in and closes the listener.</param>
    /// <returns>The consent URL and the completion, or SND-CONN-006-style failure when no port can be listened on.</returns>
    public ValueTask<Result<SignInStarted>> Start(ConnectionPreset preset, string client, IReadOnlyList<string> scopes, string? clientSecret, CancellationToken cancellationToken);

    /// <summary>
    /// Starts the sign-in of an MCP server (connections PR 10b): the server's own OAuth metadata, a client registered on the spot (RFC 7591) and the same
    /// loopback callback, state check and 5-minute limit as <see cref="Start"/>. The client id it registered is not secret and is not returned.
    /// </summary>
    /// <param name="server">The MCP server's URL (https, or http on a loopback address).</param>
    /// <param name="cancellationToken">Cancels the sign-in and closes the listener.</param>
    /// <returns>The consent URL and the completion, or the failure when the server offers no sign-in.</returns>
    public ValueTask<Result<SignInStarted>> StartMcp(Uri server, CancellationToken cancellationToken);
}
