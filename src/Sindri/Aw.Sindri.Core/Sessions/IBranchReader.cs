namespace Aw.Sindri.Core.Sessions;

/// <summary>Reads the branch a session's worktree has checked out, from the files in its '.git' (no git process). Never throws.</summary>
public interface IBranchReader
{
    /// <summary>The branch checked out in <paramref name="directory"/>.</summary>
    /// <param name="directory">The session's working directory, or a folder below a worktree's root.</param>
    /// <returns>The branch name; null for a detached HEAD, no repository or a file that can't be read.</returns>
    public string? Branch(string directory);
}
