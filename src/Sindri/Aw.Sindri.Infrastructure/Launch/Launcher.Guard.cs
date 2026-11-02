using Aw.Kernel;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>The worktree checks before a Step's files are written (the part of <see cref="Launcher"/> that guards, trusts and prepares the worktree).</summary>
internal sealed partial class Launcher
{
    /// <summary>The pre-push guard and the trust mark, then the host-side prepare; the first refusal, else null.</summary>
    private async ValueTask<Error?> GuardAndPrepare(LaunchRequest request, AgentEntry entry, (string WorkDir, bool Created) worktree, CancellationToken cancellationToken) =>
        await Guard(request, entry, worktree.WorkDir, cancellationToken).ConfigureAwait(false) ?? await PrepareWorkspace(request, worktree.WorkDir, worktree.Created, cancellationToken).ConfigureAwait(false);

    /// <summary>A Step's worktree gets the pre-push guard and, for claude-code, its trust mark before anything else is written; an error when either is refused.</summary>
    private async ValueTask<Error?> Guard(LaunchRequest request, AgentEntry entry, string workDir, CancellationToken cancellationToken)
    {
        if (!request.Attended && request.Step is not null && await PrePushGuard.Install(workDir, cancellationToken).ConfigureAwait(false) is { } foreign)
        {
            return foreign;
        }

        return await Trust(request, entry, workDir, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>A claude-code Step's worktree is marked trusted before the CLI starts: it would stop at "do you trust this project?" with no one to answer. Other entries and human opens need nothing.</summary>
    private async ValueTask<Error?> Trust(LaunchRequest request, AgentEntry entry, string workDir, CancellationToken cancellationToken)
    {
        if (request.Attended || !string.Equals(entry.Id.Value, "claude-code", StringComparison.Ordinal))
        {
            return null;
        }

        var trusted = await parts.Trust.Trust(workDir, cancellationToken).ConfigureAwait(false);
        return trusted.IsError ? trusted.Error : null;
    }

    /// <summary>
    /// The host-side prepare (sandbox spec §3.8, SB-11) of a Step's own worktree: after the guard and before the sandbox, outside the session cap (it queues on the heavy lock). An attended open, a Step with no
    /// worktree (it works in the owner's checkout) and the evaluator are never prepared. A launch with no known base commit reaches the prepare with none, and the prepare then skips it: the worktree's lockfile is never read.
    /// </summary>
    private async ValueTask<Error?> PrepareWorkspace(LaunchRequest request, string workDir, bool created, CancellationToken cancellationToken)
    {
        if (parts.Prepare is not { } prepare || request.Attended || request.Step is null || request.Worktree is null || request.Evaluator is not null)
        {
            return null;
        }

        var baseCommit = await RunBaseCommits.Resolve(request, Path.Combine(parts.Options.LaunchDirectory, "bases"), workDir, created, cancellationToken).ConfigureAwait(false);
        // why: a null base (the worktree pre-dated the run's first launch and the engine named no commit) makes the prepare skip silently: HEAD or the worktree's lockfile may hold an agent's edit, and the launcher has no logger
        var prepared = await prepare.Prepare(request with { BaseCommit = baseCommit }, workDir, progress: null, cancellationToken).ConfigureAwait(false);   // progress: no launch progress channel exists yet
        return prepared.IsError ? prepared.Error : null;
    }
}
