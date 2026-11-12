namespace Aw.Sindri.Core.Setup;

/// <summary>
/// What a profile repo's path holds (#3371). A plain folder, or a folder inside another checkout, is no checkout: the context map leaves it out
/// and names it, where git would fail every read of it (SND-CONTEXT-048) or read the parent's commits. A missing folder stays in the map, so its
/// reads keep failing loudly (an unmounted or deleted repo) and no map publishes without it.
/// </summary>
public interface IRepoCheckouts
{
    /// <summary>What the path is: the top of a git checkout (a '.git' folder, or the '.git' file of a worktree), a plain folder, or nothing.</summary>
    /// <param name="path">The repo's path as the profile writes it.</param>
    /// <returns>The kind of folder.</returns>
    public RepoCheckout Of(string path);
}
