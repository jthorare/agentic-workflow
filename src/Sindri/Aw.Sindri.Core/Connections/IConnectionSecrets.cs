using Aw.Kernel;
using Aw.Sindri.Core.Connections.OAuth;
using Aw.Sindri.Core.Secrets;

namespace Aw.Sindri.Core.Connections;

/// <summary>
/// A connection's secret: ONE keychain entry (service 'sindri-connection', account = the name with ':' as '.') holding the
/// <see cref="TokenSet"/> as JSON, base64url-encoded. The token is in no argument list, log, error or ledger row.
/// </summary>
public interface IConnectionSecrets
{
    /// <summary>Stores the tokens.</summary>
    /// <param name="name">The connection.</param>
    /// <param name="tokens">The tokens.</param>
    /// <returns>The pointer for the row, or SND-CONN-006.</returns>
    public Result<SecretPointer> Store(ConnectionName name, TokenSet tokens);

    /// <summary>Reads the tokens.</summary>
    /// <param name="location">The row's pointer.</param>
    /// <returns>The tokens, or SND-CONN-006.</returns>
    public Result<TokenSet> Read(SecretPointer location);

    /// <summary>Deletes the stored tokens.</summary>
    /// <param name="location">The row's pointer.</param>
    /// <returns>Unit; nothing stored counts as deleted.</returns>
    public Result<Unit> Delete(SecretPointer location);
}
