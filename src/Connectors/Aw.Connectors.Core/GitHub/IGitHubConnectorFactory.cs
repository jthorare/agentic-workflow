namespace Aw.Connectors.Core.GitHub;

/// <summary>Makes the GitHub connector for the profile's repos over a reader the caller built (the reader carries the token, which only the Host resolves).</summary>
public interface IGitHubConnectorFactory
{
    /// <summary>Makes the connector.</summary>
    /// <param name="settings">The checked 'context.sources.github' entry; its repos are the ones read, in order.</param>
    /// <param name="reader">Reads GitHub.</param>
    /// <param name="window">How far back a repo's first pull reaches.</param>
    /// <returns>A connector that pulls every listed repo, each from its own cursor.</returns>
    public IConnector Create(GitHubSettings settings, IGitHubReader reader, TimeSpan window);
}
