using Aw.Kernel;

namespace Aw.Forge.Infrastructure.Credentials;

/// <summary>Hands out the forge token. Only Sindri's services and effects hold one; agent steps never do.</summary>
internal interface ICredentialSource
{
    /// <summary>The token for a host.</summary>
    /// <param name="host">The forge host the token is for.</param>
    /// <param name="refresh">True after a 401: drop what is cached and ask again.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The token, or the failure naming why none could be had.</returns>
    public ValueTask<Result<string>> Token(string host, bool refresh, CancellationToken cancellationToken);
}
