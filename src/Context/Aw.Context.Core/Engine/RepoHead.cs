namespace Aw.Context.Core.Engine;

/// <summary>A mapped repo's checkout as a map build would read it.</summary>
/// <param name="Repo">The repo.</param>
/// <param name="Head">The commit its checkout's HEAD points at.</param>
/// <param name="OnDefault">Whether that commit is on the default branch (reachable from origin/HEAD, else main, else master): false for a feature branch checkout.</param>
public sealed record RepoHead(ContextRepoName Repo, string Head, bool OnDefault);
