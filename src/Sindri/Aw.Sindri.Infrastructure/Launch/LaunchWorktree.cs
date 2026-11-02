using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>Creates a Step's worktree when it is missing.</summary>
internal static class LaunchWorktree
{
    // A checkout of a large repo takes seconds and is never cut off: git killed mid-checkout leaves a locked,
    // half-checked-out worktree, and the next launch would find the directory and use it. It ends with the launch's own
    // cancellation.
    private static readonly TimeSpan _gitBudget = Timeout.InfiniteTimeSpan;

    internal static async ValueTask<Error?> Ensure(LaunchRequest request, CancellationToken cancellationToken)
    {
        if (request.Worktree is not { } worktree || Directory.Exists(worktree))
        {
            return null;
        }

        try
        {
            // why: an open's sandbox writes refs under its own namespace only, so its worktree starts on a branch there (sandbox spec §3.9); a Step's comes from the engine on its run branch
            string[] add = request.Attended ? ["-C", request.RepoPath, "worktree", "add", "-b", AttendedOpen.Branch(request.Launch), worktree] : ["-C", request.RepoPath, "worktree", "add", "--detach", worktree];
            if (await ViewerProcess.Output("git", add, _gitBudget, cancellationToken).ConfigureAwait(false) is not null)
            {
                return null;
            }
        }
        catch (OperationCanceledException)
        {
            await Discard(request.RepoPath, worktree).ConfigureAwait(false);
            throw;
        }

        await Discard(request.RepoPath, worktree).ConfigureAwait(false);
        return SindriErrors.LaunchRepoMissing.With($"the Step's worktree could not be created at {worktree}");
    }

    // A failed or cancelled add may leave a registered, locked worktree; removing it lets the next launch start clean.
    private static async ValueTask Discard(string repoPath, string worktree) =>
        _ = await ViewerProcess.Output("git", ["-C", repoPath, "worktree", "remove", "--force", "--force", worktree], _gitBudget, CancellationToken.None).ConfigureAwait(false);
}
