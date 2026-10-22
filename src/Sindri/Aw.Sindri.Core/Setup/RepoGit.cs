namespace Aw.Sindri.Core.Setup;

/// <summary>What git says of a checkout (setup-once spec §5.1 "Git facts"): either may be null (a repo with no commit yet).</summary>
/// <param name="DefaultBranch">The branch HEAD is on.</param>
/// <param name="Head">HEAD's commit, abbreviated.</param>
public sealed record RepoGit(string? DefaultBranch, string? Head);
