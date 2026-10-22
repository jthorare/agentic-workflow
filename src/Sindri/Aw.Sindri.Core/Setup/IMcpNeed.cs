using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Setup;

/// <summary>Whether registering Sindri in a repo would change anything (setup-once spec §5.4: unchanged providers write nothing, and an unchanged registration starts no job).</summary>
public interface IMcpNeed
{
    /// <summary>Asks what a registration would do, without writing.</summary>
    /// <param name="repo">The repo of the approved snapshot.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>True when some provider would be registered, replaced or refused; false when each is unchanged or not installed.</returns>
    public ValueTask<bool> Needed(RepoName repo, CancellationToken cancellationToken);
}
