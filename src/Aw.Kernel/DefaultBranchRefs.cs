namespace Aw.Kernel;

/// <summary>
/// The local refs that name a repo's default branch, in the order they are tried: the first that exists wins. Shared by the code index and the
/// context map hold, so one repo is on or off its default branch for both (#3928). main before master: a stale local master never wins over
/// origin/main; within each name the local branch comes first.
/// </summary>
public static class DefaultBranchRefs
{
    /// <summary>origin/HEAD, main, origin/main, master, origin/master, as full ref names.</summary>
    public static IReadOnlyList<string> InOrder { get; } = ["refs/remotes/origin/HEAD", "refs/heads/main", "refs/remotes/origin/main", "refs/heads/master", "refs/remotes/origin/master"];
}
