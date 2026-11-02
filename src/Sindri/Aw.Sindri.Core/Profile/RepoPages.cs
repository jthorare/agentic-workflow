namespace Aw.Sindri.Core.Profile;

/// <summary>The named pages that apply to one repo: the profile's <c xml:lang="en">index.pages</c> and the repo's own, which the context engine reads as page pins.</summary>
public static class RepoPages
{
    /// <summary>The entries of <paramref name="repo"/> under <paramref name="profile"/>.</summary>
    /// <param name="profile">The approved profile.</param>
    /// <param name="repo">One of its repos.</param>
    /// <returns>The profile's entries that name no repo or this one, then the repo's own; empty when neither names any.</returns>
    public static IReadOnlyList<PageSettings> For(SindriProfile profile, RepoEntry repo)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(repo);
        return [.. (profile.Index?.Pages ?? []).Where(page => page.Repo is null || string.Equals(page.Repo, repo.Name, StringComparison.Ordinal)), .. repo.Index?.Pages ?? []];
    }
}
