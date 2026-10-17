namespace Aw.Forge.Core.Pulls;

/// <summary>An explicit-sha push.</summary>
/// <param name="RepoDir">The working copy or git directory holding the commit.</param>
/// <param name="Sha">The commit to push.</param>
/// <param name="Remote">The profile's URL for the repository (never read from the repo's config).</param>
/// <param name="Branch">The branch to move.</param>
/// <param name="LeaseSha">The tip the branch must still have, or null when the branch must not exist yet.</param>
public sealed record PushRequest(string RepoDir, string Sha, string Remote, string Branch, string? LeaseSha);
