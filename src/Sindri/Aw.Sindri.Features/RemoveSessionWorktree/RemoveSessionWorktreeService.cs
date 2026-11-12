using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Worktrees;
using Aw.Sindri.Features.StartSession;

namespace Aw.Sindri.Features.RemoveSessionWorktree;

/// <summary>
/// Removes the one-off worktree of a session (agents spec §15.8): only a path a start of this act id recorded can be removed, and 'git worktree remove' never gets --force. A tree with changes, an act id no start
/// recorded and a start that made no worktree are SND-AGENT-018; the message carries the 'git status' summary when there is one.
/// </summary>
/// <param name="book">The stored starts.</param>
/// <param name="worktrees">Removes the worktree.</param>
internal sealed class RemoveSessionWorktreeService(StartedSessions book, IOneOffWorktrees worktrees) : IService<RemoveSessionWorktreeRequest, RemovedWorktree>
{
    public async ValueTask<Result<RemovedWorktree>> Handle(RemoveSessionWorktreeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var stored = await book.Find(request.ActId, cancellationToken).ConfigureAwait(false);
        if (stored?.Worktree is not { } path)
        {
            return SindriErrors.AgentWorktreeKept.With(stored is null
                ? $"no start recorded the act id {request.ActId}, so there is no worktree of it to remove"
                : "that start made no worktree");
        }

        var removed = await worktrees.Remove(path, cancellationToken).ConfigureAwait(false);
        if (removed.IsError)
        {
            var summary = await worktrees.StatusSummary(path, cancellationToken).ConfigureAwait(false);
            return SindriErrors.AgentWorktreeKept.With(summary.Length > 0 ? $"{path} has changes:\n{summary}" : $"{path}: {removed.Error.Message}");
        }

        return new RemovedWorktree(request.ActId, path);
    }
}
