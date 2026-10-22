namespace Aw.Sindri.Core.Forge;

/// <summary>A push to a pull request's head branch.</summary>
/// <param name="Link">The pull request's link.</param>
/// <param name="Workspace">The worktree holding the commit. The remote is the pull request's repository on the profile's host, never the worktree's own configuration.</param>
/// <param name="Sha">The commit to push.</param>
/// <param name="LeaseSha">The tip the branch must still have: the head the snapshot saw.</param>
public sealed record PullPush(string Link, string Workspace, string Sha, string LeaseSha);
