using Aw.Kernel;

namespace Aw.Sindri.Core.Secrets;

/// <summary>Stores a secret Sindri writes (a source token from `sindri index connect`) where `SecretPointer` can read it back. The secret never appears in an argument list, a log or an error.</summary>
public interface ISecretWriter
{
    /// <summary>Stores a secret.</summary>
    /// <param name="service">The keychain service (`sindri-index`).</param>
    /// <param name="account">The keychain account (`github`).</param>
    /// <param name="secret">The secret.</param>
    /// <returns>The pointer to put in the profile (`keychain:sindri-index/github`, or a `file:` pointer when no store is available), or the error when neither store can be written.</returns>
    public Result<SecretPointer> Store(string service, string account, string secret);
}
