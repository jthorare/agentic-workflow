namespace Aw.Sindri.Core.Profile;

/// <summary>The deny globs that apply to one repo: the profile's <c xml:lang="en">index.denyPaths</c>, and the repo's own <c xml:lang="en">index.denyPaths</c> and the files of its own ignored <c xml:lang="en">index.roles</c> (roles are per repo only; a demoted role is indexed, see <see cref="RepoDemoteGlobs"/>), which the context engine adds to its built-in secret globs.</summary>
public static class RepoDenyGlobs
{
    /// <summary>The globs of <paramref name="repo"/> under <paramref name="profile"/>.</summary>
    /// <param name="profile">The approved profile.</param>
    /// <param name="repo">One of its repos.</param>
    /// <returns>The globs, or null when neither names any.</returns>
    public static IReadOnlyList<string>? For(SindriProfile profile, RepoEntry repo)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(repo);
        string[] globs = [.. profile.Index?.DenyPaths ?? [], .. repo.Index?.DenyPaths ?? [], .. RoleGlobs(repo.Index)];
        return globs.Length == 0 ? null : globs;
    }

    // A role is a name for a list of globs; an ignored role (in index.ignore, or in neither list) is denied like any other glob, roles in name order so the list is stable. There are no built-in roles.
    private static IEnumerable<string> RoleGlobs(IndexSettings? index) =>
        (index?.Roles ?? new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal))
            .Where(r => index?.Demote?.Contains(r.Key, StringComparer.Ordinal) != true)
            .OrderBy(r => r.Key, StringComparer.Ordinal).SelectMany(r => r.Value);

    /// <summary>Every repo of <paramref name="profile"/> with its globs: the one mapping from the profile to the scope the context engine reads (every surface and the doctor use it).</summary>
    /// <param name="profile">The approved profile.</param>
    /// <returns>Each repo's name, path and globs (null when none), in profile order.</returns>
    public static IReadOnlyList<(string Name, string Path, IReadOnlyList<string>? DenyPaths)> Entries(SindriProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return [.. (profile.Repos ?? []).Select(r => (r.Name, r.Path, For(profile, r)))];
    }
}
