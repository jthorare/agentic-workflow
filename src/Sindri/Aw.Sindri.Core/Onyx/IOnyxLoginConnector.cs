using Aw.Kernel;

namespace Aw.Sindri.Core.Onyx;

/// <summary>Runs Onyx setup steps 6 to 9 with the owner's login in hand (the app's form gave it); the setup job's way to them without naming the account slice.</summary>
public interface IOnyxLoginConnector
{
    /// <summary>Makes Sindri's user and token with the login and writes the profile section.</summary>
    /// <param name="port">Onyx's loopback port.</param>
    /// <param name="version">The release the running Onyx reported.</param>
    /// <param name="login">The owner's login; used for the key and forgotten.</param>
    /// <param name="cancellationToken">Cancels the requests.</param>
    /// <returns>What the steps did, or the error that stopped them.</returns>
    public ValueTask<Result<OnyxConnected>> Connect(int port, string version, OnyxLogin login, CancellationToken cancellationToken);
}
