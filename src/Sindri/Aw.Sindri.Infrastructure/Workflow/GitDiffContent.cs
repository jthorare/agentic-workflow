using Aw.Kernel;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary><see cref="IDiffContent"/> through git: the committed change from the base to HEAD, without rename detection so a moved file's lines count as added.</summary>
internal sealed class GitDiffContent : IDiffContent
{
    public async ValueTask<Result<IReadOnlyList<(string Path, int Line, string Text)>>> AddedLines(string worktree, string baseCommit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(worktree);
        ArgumentNullException.ThrowIfNull(baseCommit);
        var run = await GitProcess.Run(worktree, ["-c", "core.quotepath=false", "diff", "--no-color", "--unified=0", "--no-renames", "--no-ext-diff", "--end-of-options", baseCommit, "HEAD", "--"], cancellationToken).ConfigureAwait(false);
        return run.Exit == 0
            ? Results.Success(DiffAddedLines.Parse(run.Output))
            : WorkflowErrors.EffectFailed.With($"git diff {baseCommit} HEAD failed in {worktree}: {run.Error.Trim()}");
    }

    public async ValueTask<Result<IReadOnlyList<string>>> DeletedPaths(string worktree, string baseCommit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(worktree);
        ArgumentNullException.ThrowIfNull(baseCommit);
        var run = await GitProcess.Run(worktree, ["diff", "--name-only", "-z", "--no-renames", "--diff-filter=D", "--end-of-options", baseCommit, "HEAD", "--"], cancellationToken).ConfigureAwait(false);
        if (run.Exit != 0)
        {
            return WorkflowErrors.EffectFailed.With($"git diff {baseCommit} HEAD failed in {worktree}: {run.Error.Trim()}");
        }

        IReadOnlyList<string> paths = [.. run.Output.Split('\0', StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        return Results.Success(paths);
    }
}
