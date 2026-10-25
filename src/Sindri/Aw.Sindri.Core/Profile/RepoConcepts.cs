namespace Aw.Sindri.Core.Profile;

/// <summary>The concepts that apply to one repo: the profile's <c xml:lang="en">index.concepts</c> and the repo's own, which the context engine reads as pins.</summary>
public static class RepoConcepts
{
    /// <summary>The entries of <paramref name="repo"/> under <paramref name="profile"/>.</summary>
    /// <param name="profile">The approved profile.</param>
    /// <param name="repo">One of its repos.</param>
    /// <returns>The profile's entries, then the repo's own; empty when neither names any.</returns>
    public static IReadOnlyList<ConceptSettings> For(SindriProfile profile, RepoEntry repo)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(repo);
        return [.. profile.Index?.Concepts ?? [], .. repo.Index?.Concepts ?? []];
    }
}
