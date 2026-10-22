using Aw.Kernel;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine.Effects;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>
/// <see cref="IWorkspaceRebase"/> through git: fetches the base from 'origin' and rebases onto it ('--onto' the fetched tip from the old base when one is
/// named), reads the paths of a conflict and aborts it, so the worktree is left as it was.
/// </summary>
internal sealed class GitWorkspaceRebase : IWorkspaceRebase
{
    public async ValueTask<Result<RebaseOutcome>> Rebase(string workspace, string onto, string? oldBase, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(onto);
        if (onto.StartsWith('-') || oldBase?.StartsWith('-') == true)
        {
            return WorkflowErrors.EffectFailed.With("a rebase base can't start with '-'");
        }

        var before = await Git(workspace, ["rev-parse", "--verify", "HEAD"], cancellationToken).ConfigureAwait(false);
        var fetched = before.IsError ? before : await Git(workspace, ["fetch", "--no-tags", "origin", onto], cancellationToken).ConfigureAwait(false);
        if (fetched.IsError)
        {
            return fetched.Error;
        }

        string[] rebase = oldBase is null ? ["rebase", "FETCH_HEAD"] : ["rebase", "--onto", "FETCH_HEAD", oldBase];
        var run = await GitProcess.Run(workspace, rebase, cancellationToken).ConfigureAwait(false);
        return run.Exit == 0
            ? await Finished(workspace, before.Value, cancellationToken).ConfigureAwait(false)
            : await Conflicted(workspace, run.Error, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<Result<RebaseOutcome>> Finished(string workspace, string before, CancellationToken cancellationToken)
    {
        var after = await Git(workspace, ["rev-parse", "--verify", "HEAD"], cancellationToken).ConfigureAwait(false);
        if (after.IsError)
        {
            return after.Error;
        }

        return string.Equals(after.Value, before, StringComparison.Ordinal)
            ? new RebaseOutcome(RebaseStatus.Nothing, NewHead: null, [])
            : new RebaseOutcome(RebaseStatus.Rebased, after.Value, []);
    }

    private static async ValueTask<Result<RebaseOutcome>> Conflicted(string workspace, string failure, CancellationToken cancellationToken)
    {
        var unmerged = await Git(workspace, ["diff", "--name-only", "--diff-filter=U"], cancellationToken).ConfigureAwait(false);
        _ = await GitProcess.Run(workspace, ["rebase", "--abort"], cancellationToken).ConfigureAwait(false);
        if (unmerged.IsError)
        {
            return unmerged.Error;
        }

        var paths = unmerged.Value.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Order(StringComparer.Ordinal).ToList();
        return paths.Count > 0
            ? new RebaseOutcome(RebaseStatus.Conflict, NewHead: null, paths)
            : WorkflowErrors.EffectFailed.With($"git rebase failed in {workspace}: {failure.Trim()}");
    }

    private static async ValueTask<Result<string>> Git(string workspace, string[] args, CancellationToken cancellationToken)
    {
        var run = await GitProcess.Run(workspace, ["-c", "core.quotepath=false", .. args], cancellationToken).ConfigureAwait(false);
        return run.Exit == 0 ? run.Output.Trim() : WorkflowErrors.EffectFailed.With($"git {args[0]} failed in {workspace}: {run.Error.Trim()}");
    }
}
