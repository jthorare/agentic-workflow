using Aw.Kernel;
using Aw.Sindri.Core.Connections.OAuth;

namespace Aw.Sindri.Core.Connections;

/// <summary>Calls a cheap authenticated URL to prove a stored credential works.</summary>
public interface IConnectionProbe
{
    /// <summary>Sends an authenticated GET.</summary>
    /// <param name="url">The probe URL (https).</param>
    /// <param name="tokens">The credential.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Unit when the provider accepts it; otherwise an error whose text holds no token.</returns>
    public ValueTask<Result<Unit>> Probe(string url, TokenSet tokens, CancellationToken cancellationToken);
}
