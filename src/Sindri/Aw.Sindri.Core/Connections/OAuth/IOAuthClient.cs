using Aw.Kernel;

namespace Aw.Sindri.Core.Connections.OAuth;

/// <summary>The token endpoint of a provider (code exchange, refresh, revoke). An error's text never holds a token.</summary>
public interface IOAuthClient
{
    /// <summary>Trades the callback's code for tokens (with the PKCE verifier).</summary>
    /// <param name="exchange">The code, verifier and where they were asked.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The tokens, or a failure whose text holds no token.</returns>
    public ValueTask<Result<TokenSet>> ExchangeCode(CodeExchange exchange, CancellationToken cancellationToken);

    /// <summary>Gets a new access token. A rotated refresh token comes back in the set; a provider that keeps the old one gives it back unchanged.</summary>
    /// <param name="preset">The provider.</param>
    /// <param name="client">The OAuth client id.</param>
    /// <param name="refreshToken">The current refresh token.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The tokens, or SND-CONN-005.</returns>
    public ValueTask<Result<TokenSet>> Refresh(ConnectionPreset preset, string client, string refreshToken, CancellationToken cancellationToken);

    /// <summary>Asks the provider to revoke a token. A preset without a revoke endpoint succeeds without a call.</summary>
    /// <param name="preset">The provider.</param>
    /// <param name="client">The OAuth client id.</param>
    /// <param name="token">The token to revoke.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Unit, or the provider's refusal without the token.</returns>
    public ValueTask<Result<Unit>> Revoke(ConnectionPreset preset, string client, string token, CancellationToken cancellationToken);
}
