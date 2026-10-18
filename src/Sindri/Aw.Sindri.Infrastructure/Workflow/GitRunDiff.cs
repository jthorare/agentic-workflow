using Aw.Kernel;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>The run diff through git (engine P8 D10): HEAD from 'rev-parse', "nothing changed" from HEAD equal to the start and an empty 'status --porcelain' (staged, unstaged and untracked all count).</summary>
internal sealed class GitRunDiff : IRunDiff
{
    public async ValueTask<Result<string>> Head(string worktree, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(worktree);
        var run = await GitProcess.Run(worktree, ["rev-parse", "--verify", "HEAD"], cancellationToken, inWorktree: true).ConfigureAwait(false);
        return run.Exit == 0 ? run.Output : WorkflowErrors.EffectFailed.With($"git rev-parse HEAD failed in {worktree}: {run.Error.Trim()}");
    }

    public async ValueTask<Result<bool>> IsEmpty(string worktree, string startHead, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(startHead);
        var head = await Head(worktree, cancellationToken).ConfigureAwait(false);
        if (head.IsError)
        {
            return head.Error;
        }

        if (!string.Equals(head.Value, startHead, StringComparison.Ordinal))
        {
            return false;
        }

        var status = await GitProcess.Run(worktree, ["status", "--porcelain", "--untracked-files=all"], cancellationToken, inWorktree: true).ConfigureAwait(false);
        return status.Exit == 0 ? status.Output.Length == 0 : WorkflowErrors.EffectFailed.With($"git status failed in {worktree}: {status.Error.Trim()}");
    }
}
