using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Skills;

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
            string[] start = IsTrial(request) && await DefaultBranch(request.RepoPath, cancellationToken).ConfigureAwait(false) is { } trunk ? [trunk] : [];   // a skill trial starts from the repo's default branch, not whatever HEAD is
            string[] add = request.Attended ? ["-C", request.RepoPath, "worktree", "add", "-b", AttendedOpen.Branch(request.Launch), worktree] : ["-C", request.RepoPath, "worktree", "add", "--detach", worktree, .. start];
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

    /// <summary>Whether the launch is a skill trial's Step (its instance is a 'skill-trial' recipe's).</summary>
    internal static bool IsTrial(LaunchRequest request) => !request.Attended && request.Instance is { } instance && SkillTrialRecipe.IsTrial(instance);

    /// <summary>The origin's HEAD branch ('origin/main'), else a local 'main'; null when neither is there (the worktree then starts at HEAD).</summary>
    internal static async ValueTask<string?> DefaultBranch(string repoPath, CancellationToken cancellationToken)
    {
        foreach (var candidate in new[] { await ViewerProcess.Output("git", ["-C", repoPath, "symbolic-ref", "--short", "refs/remotes/origin/HEAD"], _gitBudget, cancellationToken).ConfigureAwait(false), "main" })
        {
            var name = candidate?.Trim();
            if (!string.IsNullOrEmpty(name) && await ViewerProcess.Output("git", ["-C", repoPath, "rev-parse", "--verify", "--quiet", name + "^{commit}"], _gitBudget, cancellationToken).ConfigureAwait(false) is not null)
            {
                return name;
            }
        }

        return null;
    }

    /// <summary>Removes a trial's worktree with plain 'git worktree remove' (never forced): a tree with changes in it, or commits on no branch (it is detached, so a plain remove would drop them), is kept, and the warning names it so the agent's work is never lost.</summary>
    /// <param name="repoPath">The repo.</param>
    /// <param name="worktree">The worktree.</param>
    /// <param name="warn">Where the one warning line goes.</param>
    internal static async ValueTask Remove(string repoPath, string worktree, Action<string> warn)
    {
        if (await ViewerProcess.Output("git", ["-C", worktree, "rev-list", "--count", "HEAD", "--not", "--branches", "--remotes"], _gitBudget, CancellationToken.None).ConfigureAwait(false) is { } count
            && int.TryParse(count.Trim(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var commits) && commits > 0)
        {
            warn($"warn: the skill trial's worktree {worktree} was kept: it holds {commits.ToString(System.Globalization.CultureInfo.InvariantCulture)} commit{(commits == 1 ? string.Empty : "s")} that are on no branch; branch them or remove it by hand with git worktree remove --force when you have looked");   // why: the trial worktree is detached, so removing it would drop them
            return;
        }

        var removed = await ViewerProcess.Output("git", ["-C", repoPath, "worktree", "remove", worktree], _gitBudget, CancellationToken.None).ConfigureAwait(false);
        if (removed is null && Directory.Exists(worktree))
        {
            warn($"warn: the skill trial's worktree {worktree} was kept: git would not remove it (changes in it?); remove it by hand with git worktree remove --force when you have looked");
        }
    }

    // A failed or cancelled add may leave a registered, locked worktree; removing it lets the next launch start clean.
    private static async ValueTask Discard(string repoPath, string worktree) =>
        _ = await ViewerProcess.Output("git", ["-C", repoPath, "worktree", "remove", "--force", "--force", worktree], _gitBudget, CancellationToken.None).ConfigureAwait(false);
}
