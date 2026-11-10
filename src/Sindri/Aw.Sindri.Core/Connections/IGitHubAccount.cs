using Aw.Kernel;

namespace Aw.Sindri.Core.Connections;

/// <summary>The owner's GitHub connection as the Reviews tab sees it: its state, name and /user login.</summary>
public interface IGitHubAccount
{
    /// <summary>Reads the account.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The account, or the store's error.</returns>
    public ValueTask<Result<GitHubAccount>> Current(CancellationToken cancellationToken);
}
