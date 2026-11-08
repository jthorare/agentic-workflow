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
    /// <param name="cancellationToken">Cancels the sign-in and closes the listener.</param>
    /// <returns>The consent URL and the completion, or SND-CONN-006-style failure when no port can be listened on.</returns>
    public ValueTask<Result<SignInStarted>> Start(ConnectionPreset preset, string client, IReadOnlyList<string> scopes, CancellationToken cancellationToken);
}
