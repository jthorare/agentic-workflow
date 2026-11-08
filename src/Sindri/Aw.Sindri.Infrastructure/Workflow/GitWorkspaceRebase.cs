using Aw.Kernel;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>
/// <see cref="IWorkspaceRebase"/> through git, hardened like the other adapters that run in a checkout the engine did not write (no hooks, no fsmonitor, no system config). The fetch goes to the workspace's
/// own 'origin' and reads one branch; the push is not here, it is the forge port's. A rebase that conflicts is aborted, so the workspace is back on its head and no rebase is under way.
/// </summary>
internal sealed class GitWorkspaceRebase : IWorkspaceRebase
{
    public async ValueTask<Result<string>> Fetch(string worktree, string branch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(worktree);
        ArgumentNullException.ThrowIfNull(branch);
        if (!IsBranch(branch))
        {
            return WorkflowErrors.EffectFailed.With($"'{branch}' is not a branch name git may fetch (letters, digits, '/', '.', '_' and '-' only)");
        }

        var fetched = await Git(worktree, ["fetch", "--quiet", "--no-tags", "origin", "refs/heads/" + branch], cancellationToken).ConfigureAwait(false);
        return fetched.IsError ? fetched.Error : await Git(worktree, ["rev-parse", "--verify", "--end-of-options", "FETCH_HEAD^{commit}"], cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<Result<bool>> Contains(string worktree, string ancestor, string descendant, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(worktree);
        ArgumentNullException.ThrowIfNull(ancestor);
        ArgumentNullException.ThrowIfNull(descendant);
        var run = await GitProcess.Run(worktree, ["merge-base", "--is-ancestor", "--end-of-options", ancestor, descendant], cancellationToken, hardened: true).ConfigureAwait(false);
        return run.Exit switch
        {
            0 => true,
            1 => false,
            _ => WorkflowErrors.EffectFailed.With($"git merge-base failed in {worktree}: {run.Error.Trim()}"),
        };
    }

    public async ValueTask<Result<WorkspaceRebased>> Rebase(string worktree, string onto, string? from, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(worktree);
        ArgumentNullException.ThrowIfNull(onto);
        var identity = await Identity(worktree, cancellationToken).ConfigureAwait(false);
        string[] arguments = from is null
            ? [.. identity, "rebase", "--quiet", "--end-of-options", onto]
            : [.. identity, "rebase", "--quiet", "--onto", onto, "--end-of-options", from];
        var run = await GitProcess.Run(worktree, arguments, cancellationToken, hardened: true).ConfigureAwait(false);
        return run.Exit == 0 ? await Landed(worktree, cancellationToken).ConfigureAwait(false) : await Abandoned(worktree, run.Error, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<Result<Unit>> Reset(string worktree, string sha, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(worktree);
        ArgumentNullException.ThrowIfNull(sha);
        if (sha.Length is not (40 or 64) || !sha.All(char.IsAsciiHexDigit))   // why: 'reset' takes no end-of-options marker, so only a commit id is let through
        {
            return WorkflowErrors.EffectFailed.With($"'{sha}' is not a commit id");
        }

        var reset = await Git(worktree, ["reset", "--quiet", "--hard", sha], cancellationToken).ConfigureAwait(false);
        return reset.IsError ? reset.Error : Unit.Value;
    }

    private static async ValueTask<Result<WorkspaceRebased>> Landed(string worktree, CancellationToken cancellationToken)
    {
        var head = await Git(worktree, ["rev-parse", "--verify", "HEAD"], cancellationToken).ConfigureAwait(false);
        return head.IsError ? head.Error : new WorkspaceRebased(head.Value);
    }

    // A conflict names its unmerged paths and is aborted; a failure with none is git's own and is an error.
    private static async ValueTask<Result<WorkspaceRebased>> Abandoned(string worktree, string error, CancellationToken cancellationToken)
    {
        var unmerged = await GitProcess.Run(worktree, ["diff", "--name-only", "--diff-filter=U", "-z"], cancellationToken, hardened: true).ConfigureAwait(false);
        IReadOnlyList<string> paths = unmerged.Exit == 0 ? [.. unmerged.Output.Split('\0', StringSplitOptions.RemoveEmptyEntries).Order(StringComparer.Ordinal)] : [];
        var aborted = await GitProcess.Run(worktree, ["rebase", "--abort"], cancellationToken, hardened: true).ConfigureAwait(false);
        if (aborted.Exit != 0)
        {
            return WorkflowErrors.EffectFailed.With($"git rebase failed in {worktree} and could not be aborted: {aborted.Error.Trim()}");
        }

        return paths.Count > 0 ? new WorkspaceRebased(paths) : WorkflowErrors.EffectFailed.With($"git rebase failed in {worktree}: {error.Trim()}");
    }

    // A rebase commits, so it needs an identity; the workspace's own wins, and Sindri's stands in only when there is none.
    private static async ValueTask<string[]> Identity(string worktree, CancellationToken cancellationToken)
    {
        var email = await GitProcess.Run(worktree, ["config", "--get", "user.email"], cancellationToken, hardened: true).ConfigureAwait(false);
        var name = await GitProcess.Run(worktree, ["config", "--get", "user.name"], cancellationToken, hardened: true).ConfigureAwait(false);
        return email.Exit == 0 && name.Exit == 0 ? [] : ["-c", "user.email=sindri@localhost", "-c", "user.name=Sindri"];
    }

    private static async ValueTask<Result<string>> Git(string worktree, string[] args, CancellationToken cancellationToken)
    {
        var run = await GitProcess.Run(worktree, args, cancellationToken, hardened: true).ConfigureAwait(false);
        return run.Exit == 0 ? run.Output : WorkflowErrors.EffectFailed.With($"git {args[0]} failed in {worktree}: {run.Error.Trim()}");
    }

    private static bool IsBranch(string branch) =>
        branch.Length > 0 && !branch.StartsWith('-') && !branch.Contains("..", StringComparison.Ordinal)
        && branch.All(static c => char.IsAsciiLetterOrDigit(c) || c is '/' or '.' or '_' or '-');
}
