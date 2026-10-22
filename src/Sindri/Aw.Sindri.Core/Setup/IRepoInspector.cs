using Aw.Kernel;

namespace Aw.Sindri.Core.Setup;

/// <summary>
/// Looks at a folder with git (setup-once spec §5.1): git runs hardened ('-c core.fsmonitor= -c core.hooksPath=/dev/null', 'GIT_CONFIG_NOSYSTEM=1'), arguments as an argv, never a shell string.
/// </summary>
public interface IRepoInspector
{
    /// <summary>The cheap checks of AddRepo: the folder exists, is readable, is a git checkout, resolves to its main checkout, is not '/', the user's home, inside the Sindri home or a parent of it.</summary>
    /// <param name="path">The folder the owner picked.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>The checkout and its git facts, or SND-SETUP-001 with the reason in the owner's words.</returns>
    public ValueTask<Result<InspectedRepo>> Inspect(string path, CancellationToken cancellationToken);

    /// <summary>The git facts of a checkout the profile already names.</summary>
    /// <param name="checkout">The checkout's path.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The facts, or SND-SETUP-001 when the folder is gone or no longer a checkout.</returns>
    public ValueTask<Result<RepoGit>> Facts(string checkout, CancellationToken cancellationToken);
}
