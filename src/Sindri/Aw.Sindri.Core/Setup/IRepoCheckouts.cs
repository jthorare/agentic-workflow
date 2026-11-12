namespace Aw.Sindri.Core.Setup;

/// <summary>
/// Whether a profile repo's path is a git checkout (#3371). A plain folder, or a folder inside another checkout, is not: the context map leaves it out
/// and names it, where git would fail every read of it (SND-CONTEXT-048) or read the parent's commits.
/// </summary>
public interface IRepoCheckouts
{
    /// <summary>True when the path is the top of a git checkout (a '.git' folder, or the '.git' file of a worktree).</summary>
    /// <param name="path">The repo's path as the profile writes it.</param>
    /// <returns>True for a checkout.</returns>
    public bool IsCheckout(string path);
}
