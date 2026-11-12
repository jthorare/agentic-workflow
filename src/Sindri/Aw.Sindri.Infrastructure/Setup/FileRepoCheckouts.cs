using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Infrastructure.Setup;

/// <summary>
/// A repo path is a checkout when it holds '.git': a folder for a clone, a file for a worktree (#3371). No git process runs, so every context read can ask.
/// A plain folder inside another checkout has no '.git' of its own and is not one, where git would read the parent's commits.
/// </summary>
internal sealed class FileRepoCheckouts : IRepoCheckouts
{
    public bool IsCheckout(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var git = Path.Combine(path, ".git");
        return Directory.Exists(git) || File.Exists(git);
    }
}
