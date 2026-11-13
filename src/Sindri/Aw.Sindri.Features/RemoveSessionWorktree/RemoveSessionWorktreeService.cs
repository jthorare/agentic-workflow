using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Worktrees;

namespace Aw.Sindri.Features.RemoveSessionWorktree;

/// <summary>
/// Removes the one-off worktree of a session (agents spec §15.8): only a path a start of this act id recorded can be removed, and 'git worktree remove' never gets --force. A tree with changes, an act id no start
/// recorded, a start that made no worktree and a session whose agent still runs are SND-AGENT-018; the message carries the 'git status' summary when there is one.
/// </summary>
/// <param name="book">The stored starts.</param>
/// <param name="worktrees">Removes the worktree.</param>
/// <param name="callers">The caller: only the person may remove it.</param>
/// <param name="panes">Whether the session still runs: a running agent keeps its worktree (#3466).</param>
internal sealed class RemoveSessionWorktreeService(IStartedSessions book, IOneOffWorktrees worktrees, ICallerContextAccessor callers, ISessionPanes panes) : IService<RemoveSessionWorktreeRequest, RemovedWorktree>
{
    public async ValueTask<Result<RemovedWorktree>> Handle(RemoveSessionWorktreeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is not { Class: CallerClass.Human })
        {
            return SindriErrors.AuthRefused.With("removing a worktree is for the person: use the app or 'sindri agents worktree remove' in a terminal");
        }

        var stored = await book.Find(request.ActId, cancellationToken).ConfigureAwait(false);
        if (stored?.Worktree is not { } path)
        {
            return SindriErrors.AgentWorktreeKept.With(stored is null
                ? $"no start recorded the act id {request.ActId.Value}, so there is no worktree of it to remove"
                : "that start made no worktree");
        }

        var running = await panes.Running(new HostRef(HostKind.Tmux, "sindri-" + request.ActId.Value), cancellationToken).ConfigureAwait(false);
        if (running != false)
        {
            return SindriErrors.AgentWorktreeKept.With(running is null
                ? $"can't tell if sindri-{request.ActId.Value} is still running: tmux did not answer; {path} stays (try again)"
                : $"session still running: end sindri-{request.ActId.Value} first, then remove {path}");
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
