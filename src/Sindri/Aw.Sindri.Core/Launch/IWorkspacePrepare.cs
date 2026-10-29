using Aw.Kernel;

namespace Aw.Sindri.Core.Launch;

/// <summary>The host-side prepare of a Step's worktree (sandbox spec §3.8, SB-11): the repo's declared package directories are installed on the host, as the owner, before the sandboxed session starts.</summary>
public interface IWorkspacePrepare
{
    /// <summary>Prepares <paramref name="workDirectory"/> for the launch; does nothing for a repo that declares no prepare.</summary>
    /// <param name="request">The launch; <see cref="LaunchRequest.BaseCommit"/> names the commit the lockfiles are read at.</param>
    /// <param name="workDirectory">The worktree the session will work in.</param>
    /// <param name="progress">Told one line while the prepare waits ('Preparing the workspace'), or null.</param>
    /// <param name="cancellationToken">Cancels the prepare.</param>
    /// <returns>Success, or SND-LAUNCH-021 with its reason.</returns>
    public ValueTask<Result<Unit>> Prepare(LaunchRequest request, string workDirectory, IProgress<string>? progress, CancellationToken cancellationToken);
}
