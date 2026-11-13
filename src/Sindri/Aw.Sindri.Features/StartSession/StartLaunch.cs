using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Worktrees;

namespace Aw.Sindri.Features.StartSession;

/// <summary>
/// Makes what a start needs (agents spec §15.8): the one-off worktree when asked (60 s), then 'sindri-&lt;actId&gt;' on the Sindri tmux server (4 s) with a clean environment, then its pane. A failure after the
/// worktree was made removes the half-made session and worktree. A host error of kind Conflict comes back as it is (a start that went through earlier); every other failure is SND-AGENT-014.
/// </summary>
/// <param name="worktrees">Makes and removes the one-off worktree.</param>
/// <param name="host">The tmux session host.</param>
/// <param name="panes">The pane of a started session.</param>
/// <param name="environment">The variables a session may inherit.</param>
/// <param name="clock">The clock for the worktree name.</param>
internal sealed class StartLaunch(IOneOffWorktrees worktrees, ISessionHost host, ISessionPanes panes, IEnvironmentReader environment, TimeProvider clock)
{
    private static readonly TimeSpan _worktreeDeadline = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan _startDeadline = TimeSpan.FromSeconds(4);
    private static readonly HostSize _size = new(120, 40);

    internal async ValueTask<Result<StartedEntry>> Run(StartSessionRequest request, StartPlan plan, string actId, CancellationToken cancellationToken)
    {
        var made = await Worktree(request, plan.RepoPath, cancellationToken).ConfigureAwait(false);
        if (made.IsError)
        {
            return made.Error;
        }

        var session = new HostSession("sindri-" + actId, made.Value?.Path ?? plan.RepoPath, AgentEnvironment.Argv(plan.Agent, request.Prompt), AgentEnvironment.Of(environment, request.Agent))
        {
            Size = _size,
            RemainOnExit = true,
            CleanEnvironment = true,
            Agent = request.Agent,
            RepoPath = plan.RepoPath,
        };
        var started = await host.Start(session, _startDeadline, cancellationToken).ConfigureAwait(false);
        if (started.IsError)
        {
            await Undo(made.Value, started: null).ConfigureAwait(false);
            return started.Error.Kind == ErrorKind.Conflict ? started.Error : SindriErrors.AgentStartFailed.With(started.Error.Message);
        }

        var found = await panes.PaneId(started.Value, cancellationToken).ConfigureAwait(false);
        if (found is not { } pane)
        {
            await Undo(made.Value, started.Value).ConfigureAwait(false);
            return SindriErrors.AgentStartFailed.With("tmux started the session but did not return its pane");
        }

        return new StartedEntry(pane, made.Value?.Path);
    }

    private async ValueTask<Result<OneOffWorktree?>> Worktree(StartSessionRequest request, string repoPath, CancellationToken cancellationToken)
    {
        if (!request.Worktree)
        {
            return (OneOffWorktree?)null;
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_worktreeDeadline);
        try
        {
            var made = await worktrees.Make(repoPath, OneOffName.Of(clock.GetLocalNow(), request.Prompt, request.Agent), deadline.Token).ConfigureAwait(false);
            return made.IsError
                ? SindriErrors.AgentStartFailed.With("the worktree could not be made: " + made.Error.Message)
                : made.Value;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return SindriErrors.AgentStartFailed.With("the worktree was not made within 60 s");
        }
    }

    /// <summary>Removes what a failed start made: the session if it came up, then the worktree (git refuses a tree with changes; a fresh one has none).</summary>
    private async ValueTask Undo(OneOffWorktree? made, HostRef? started)
    {
        if (started is not null)
        {
            _ = await host.Stop(started, CancellationToken.None).ConfigureAwait(false);
        }

        if (made is not null)
        {
            _ = await worktrees.Remove(made.Path, CancellationToken.None).ConfigureAwait(false);
        }
    }
}
