namespace Aw.Sindri.Core.Profile;

/// <summary>The globs of a repo's demoted roles (<c xml:lang="en">index.demote</c>): their files stay indexed and searchable, and the context engine reads the globs as <c xml:lang="en">ContextRepo.DemotePaths</c>.</summary>
public static class RepoDemoteGlobs
{
    /// <summary>The demoted globs of <paramref name="repo"/>.</summary>
    /// <param name="profile">The approved profile.</param>
    /// <param name="repo">One of its repos.</param>
    /// <returns>The globs of each demoted role, roles in name order; or null when the repo demotes none.</returns>
    public static IReadOnlyList<string>? For(SindriProfile profile, RepoEntry repo)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(repo);
        var roles = repo.Index?.Roles;
        if (roles is null || repo.Index?.Demote is not { } demoted)
        {
            return null;
        }

        string[] globs = [.. roles.Where(r => demoted.Contains(r.Key, StringComparer.Ordinal)).OrderBy(r => r.Key, StringComparer.Ordinal).SelectMany(r => r.Value)];
        return globs.Length == 0 ? null : globs;
    }
}
