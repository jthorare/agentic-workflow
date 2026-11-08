using Aw.Context.Core.Engine;
using Aw.Context.Core.Records;

namespace Aw.Sindri.Host.IndexSources;

/// <summary>
/// Which profile repo a GitHub 'owner/name' is (owned-index spec §4.4, §7.5): the repo has to be listed in 'context.sources.github.repos' and the profile has to have a repo of that name; anything else
/// has no match and its records are dropped (default deny). The audience of a match is 'repo:&lt;owner&gt;/&lt;name&gt;'.
/// </summary>
internal sealed class ProfileRepoMap
{
    private readonly Dictionary<string, (ContextRepoName Repo, AudienceKey Audience)> _byGitHub = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Builds the map.</summary>
    /// <param name="listed">'context.sources.github.repos', each 'owner/name'.</param>
    /// <param name="profileRepos">The names of the approved profile's repos.</param>
    internal ProfileRepoMap(IEnumerable<string> listed, IEnumerable<ContextRepoName> profileRepos)
    {
        ArgumentNullException.ThrowIfNull(listed);
        ArgumentNullException.ThrowIfNull(profileRepos);
        var names = profileRepos.ToDictionary(static r => r.Value, StringComparer.OrdinalIgnoreCase);
        foreach (var gitHub in listed)
        {
            var slash = gitHub.IndexOf('/', StringComparison.Ordinal);
            if (slash > 0 && names.TryGetValue(gitHub[(slash + 1)..], out var repo) && AudienceKey.TryFrom("repo:" + gitHub, out var audience))
            {
                _byGitHub[gitHub] = (repo, audience);
            }
        }
    }

    /// <summary>The profile repo and audience of a GitHub repo.</summary>
    /// <param name="gitHubRepo">'owner/name'.</param>
    /// <returns>The match, or null when the repo isn't listed or the profile has no repo of that name.</returns>
    internal (ContextRepoName Repo, AudienceKey Audience)? Find(string gitHubRepo) => _byGitHub.TryGetValue(gitHubRepo, out var found) ? found : null;

    /// <summary>Places a GitHub record: its profile repo, its audience and the connector's path prefix 'owner/name'.</summary>
    /// <param name="item">The record.</param>
    /// <returns>The placement, or null to skip the record (no repo, an unlisted repo, no profile repo of that name).</returns>
    internal Placement? Place(Connectors.Core.ConnectorItem item) =>
        item.Repo is { } gitHubRepo && Find(gitHubRepo) is { } match ? new Placement(match.Repo, match.Audience, gitHubRepo) : null;
}
