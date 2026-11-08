using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Core.Launches;

/// <summary>Which agent an open uses when the person didn't pick one (X15, launcher L-14). Pure.</summary>
public static class LaunchTargetChoice
{
    /// <summary>The agent when nothing else is chosen.</summary>
    public static AgentId Fallback { get; } = AgentId.From("claude-code");

    /// <summary>The first of the repo's last used agent, its profile default, the profile's launch default and Claude Code that the catalog has: a rule whose agent is absent from this box's catalog is skipped.</summary>
    /// <param name="catalog">This box's catalog.</param>
    /// <param name="launch">The approved profile's 'launch:' block, or null.</param>
    /// <param name="repo">The item's repo, or null.</param>
    /// <param name="lastUsed">The agent the repo's last started launch used, or null.</param>
    /// <returns>The agent, or null when the catalog has none of them.</returns>
    public static AgentId? Choose(IAgentCatalog catalog, LaunchSettings? launch, RepoName? repo, AgentId? lastUsed)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        foreach (var candidate in Candidates(launch, repo, lastUsed))
        {
            if (catalog.Resolve(candidate) is not null)
            {
                return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<AgentId> Candidates(LaunchSettings? launch, RepoName? repo, AgentId? lastUsed)
    {
        if (lastUsed is { } last)
        {
            yield return last;
        }

        if (repo is { } named && launch?.Repos is { } repos && repos.TryGetValue(named.Value, out var settings) && settings.Default is { } repoDefault)
        {
            yield return repoDefault;
        }

        if (launch?.Default is { } profileDefault)
        {
            yield return profileDefault;
        }

        yield return Fallback;
    }
}
