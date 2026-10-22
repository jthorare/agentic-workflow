using Aw.Kernel;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Setup;

/// <summary>Starts a RegisterMcp job for a repo as the setup service does (the same command a human's press runs, so the same admission and the same claim).</summary>
public interface IMcpJobStarter
{
    /// <summary>Starts the job.</summary>
    /// <param name="repo">The repo of the approved snapshot.</param>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <returns>Unit when the runner took it, or why it refused.</returns>
    public ValueTask<Result<Unit>> Start(RepoName repo, CancellationToken cancellationToken);
}
