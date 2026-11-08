namespace Aw.Sindri.Core.Profile;

/// <summary>A repo's roles as the context engine counts them: every role of <c xml:lang="en">index.roles</c> with its globs and whether <c xml:lang="en">index.demote</c> names it (the others are ignored).</summary>
public static class RepoRoleSets
{
    /// <summary>The roles of <paramref name="repo"/>.</summary>
    /// <param name="profile">The approved profile.</param>
    /// <param name="repo">One of its repos.</param>
    /// <returns>One entry per role, in name order; or null when the repo has no roles.</returns>
    public static IReadOnlyList<ProfileRole>? For(SindriProfile profile, RepoEntry repo)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(repo);
        if (repo.Index?.Roles is not { Count: > 0 } roles)
        {
            return null;
        }

        var demoted = repo.Index.Demote ?? [];
        return [.. roles.OrderBy(static r => r.Key, StringComparer.Ordinal).Select(r => new ProfileRole(r.Key, demoted.Contains(r.Key, StringComparer.Ordinal), r.Value))];
    }
}
