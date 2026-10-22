namespace Aw.Sindri.Core.Forge;

/// <summary>A push of a branch before any pull request exists for it.</summary>
/// <param name="Repo">The repository.</param>
/// <param name="Workspace">The worktree holding the commit. The remote is the repository on the profile's host, never the worktree's own configuration.</param>
/// <param name="Branch">The branch to move or create.</param>
/// <param name="Sha">The commit to push.</param>
/// <param name="LeaseSha">The tip the branch must still have, or null when the branch must not exist yet.</param>
public sealed record PullBranchPush(PullRepo Repo, string Workspace, string Branch, string Sha, string? LeaseSha);
