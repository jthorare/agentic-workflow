using Aw.CodeIndex.Core.Values;

namespace Aw.CodeIndex.Core.Model;

/// <summary>A repo's checkout head against its default branch, from local refs only (spec §6.1).</summary>
/// <param name="Repo">The repo.</param>
/// <param name="Head">The checkout's HEAD commit, or null when it cannot be read.</param>
/// <param name="OnDefaultBranch">True when the head is the default branch's tip or one of its ancestors; a repo that is not on main is never indexed from where it is.</param>
/// <param name="DefaultTip">The default branch's tip commit from local refs (local main, else origin/main), or null when the checkout has neither. The first index of a repo that is off its default branch builds from it.</param>
public sealed record RepoHead(RepoName Repo, CommitSha? Head, bool OnDefaultBranch, CommitSha? DefaultTip = null);
