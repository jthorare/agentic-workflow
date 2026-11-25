namespace Aw.Sindri.Core.Graph.Templates;

/// <summary>Finds the CI a new workflow from a starter template can read: the box's connected GitHub connection and the profile's first repo on github.com (#3534).</summary>
public interface ICiSourceLookup
{
    /// <summary>Looks the source up. An expected miss (no connection, no repo, no remote, a remote that is not github.com, a git error) is null, never an error.</summary>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The source, or null when there is none.</returns>
    public ValueTask<GitHubCiSource?> Find(CancellationToken cancellationToken);
}
