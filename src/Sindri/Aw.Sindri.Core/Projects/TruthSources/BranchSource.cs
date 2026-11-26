namespace Aw.Sindri.Core.Projects.TruthSources;

/// <summary>A branch, or a branch-name prefix, that makes a repo's PRs count for a project (#4333): the PR's head branch equals the name, or starts with it for a prefix.</summary>
/// <param name="Repo">owner/name of the repo the branch is in.</param>
/// <param name="Name">The branch name, which may hold '/'; for a prefix, what the name starts with.</param>
/// <param name="IsPrefix">True for a prefix, false for one exact branch.</param>
public sealed record BranchSource(string Repo, string Name, bool IsPrefix);
