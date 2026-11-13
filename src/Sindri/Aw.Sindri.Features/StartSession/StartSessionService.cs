using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Worktrees;
using Aw.Sindri.Features.LinearIssues;

namespace Aw.Sindri.Features.StartSession;

/// <summary>
/// Starts a one-off session (agents spec §15.8): checks the act id, the prompt, the repo's folder, the agent and tmux; makes the worktree when asked (60 s); starts 'sindri-&lt;actId&gt;' on the Sindri tmux server
/// (4 s) with a clean environment and the prompt as one argument after '--'; and records an 'agent.op' event keyed by the act id. Only a 2xx is stored: a refusal, or a failure after the worktree was made,
/// removes the half-made session and worktree and leaves the act id free. A replayed act id after a 2xx answers the first result and starts nothing.
/// </summary>
/// <param name="approvals">The live profile, for the repo's folder.</param>
/// <param name="catalog">The agent catalog.</param>
/// <param name="checks">Whether the agent is ready and the folder exists.</param>
/// <param name="worktrees">Makes and removes the one-off worktree.</param>
/// <param name="host">The tmux session host.</param>
/// <param name="panes">The socket and pane of a started session.</param>
/// <param name="environment">The variables a session may inherit.</param>
/// <param name="choices">Remembers the last repo and agent.</param>
/// <param name="book">The stored starts.</param>
/// <param name="callers">The caller: only the person may start an agent (agents spec §15.2).</param>
/// <param name="clock">The clock for the worktree name.</param>
/// <param name="ledger">Appends the event.</param>
/// <param name="held">The lease this process holds.</param>
/// <param name="events">Builds the entry.</param>
/// <param name="gate">The command gate: the start runs outside it (a start takes up to 64 s) and takes it for the ledger write only.</param>
internal sealed partial class StartSessionService(
    IApprovedProfile approvals, IAgentCatalog catalog, IStartChecks checks, IOneOffWorktrees worktrees, ISessionHost host, ISessionPanes panes, IEnvironmentReader environment,
    IStartChoices choices, StartedSessions book, ICallerContextAccessor callers, TimeProvider clock, ILedgerStore ledger, HeldLease held, EventFactory events, ICommandGate gate)
    : IService<StartSessionRequest, StartedSession>
{
    internal const int MaxPrompt = 8000;

    private static readonly TimeSpan _worktreeDeadline = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan _startDeadline = TimeSpan.FromSeconds(4);
    private static readonly HostSize _size = new(120, 40);

    public async ValueTask<Result<StartedSession>> Handle(StartSessionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is not { Class: CallerClass.Human })
        {
            return SindriErrors.AuthRefused.With("starting an agent is for the person: use the app or 'sindri agents start' in a terminal");
        }

        if (!request.ActId.IsInitialized() || request.ActId.Value is not { } actId || !ActIdShape.IsMatch(actId))
        {
            return SindriErrors.InvalidInput.With("actId is 8 to 32 characters of a-z and 0-9");
        }

        if (request.Prompt is { Length: > MaxPrompt } prompt)
        {
            return SindriErrors.AgentPromptTooLong.With($"the prompt is {prompt.Length} characters; the most is {MaxPrompt}");
        }

        using var gate = await book.Enter(actId, cancellationToken).ConfigureAwait(false);
        if (book.InMemory(actId) is { } first)
        {
            return Answer(actId, first.Pane);   // a replay after a 2xx: the first result, nothing started
        }

        return await Start(request, actId, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<StartedSession>> Start(StartSessionRequest request, string actId, CancellationToken cancellationToken)
    {
        if (held.Current is not { } grant)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");
        }

        var repo = await RepoPath(request, cancellationToken).ConfigureAwait(false);
        if (repo.IsError)
        {
            return repo.Error;
        }

        var agent = catalog.Resolve(request.Agent);
        if (agent is null)
        {
            return SindriErrors.AgentNotReady.With($"{request.Agent.Value} is not in the agent catalog");
        }

        var ready = await checks.Agent(agent, cancellationToken).ConfigureAwait(false);
        if (!ready.Installed || !ready.SignedIn)
        {
            return SindriErrors.AgentNotReady.With($"{agent.Label} is {(ready.Installed ? "not signed in" : "not installed")} (fix: {ready.SignInCommand})").WithExtension("command", LinearChangeFields.Text(ready.SignInCommand));   // the form's Copy command button reads it
        }

        var facts = await host.Probe(cancellationToken).ConfigureAwait(false);
        if (!facts.Present)
        {
            return SindriErrors.AgentStartFailed.With($"tmux is not installed (fix: {InstallCommand()})");
        }

        var made = await Worktree(request, repo.Value, cancellationToken).ConfigureAwait(false);
        if (made.IsError)
        {
            return made.Error;
        }

        var workDir = made.Value?.Path ?? repo.Value;
        var name = "sindri-" + actId;
        var session = new HostSession(name, workDir, AgentEnvironment.Argv(agent, request.Prompt), AgentEnvironment.Of(environment, request.Agent))
        {
            Size = _size,
            RemainOnExit = true,
            CleanEnvironment = true,
            Agent = request.Agent,
            RepoPath = repo.Value,
        };
        var started = await host.Start(session, _startDeadline, cancellationToken).ConfigureAwait(false);
        if (started.IsError)
        {
            await Undo(made.Value, started: null, cancellationToken).ConfigureAwait(false);
            return await Failed(started.Error, actId, cancellationToken).ConfigureAwait(false);
        }

        var pane = await panes.FirstPane(started.Value, cancellationToken).ConfigureAwait(false);
        if (pane is null)
        {
            await Undo(made.Value, started.Value, cancellationToken).ConfigureAwait(false);
            return SindriErrors.AgentStartFailed.With("tmux started the session but did not return its pane");
        }

        book.Keep(actId, new StartedEntry(pane, made.Value?.Path));
        await Record(request, actId, pane, made.Value?.Path, grant).ConfigureAwait(false);
        await choices.Remember(request.Repo.Value, request.Agent.Value, CancellationToken.None).ConfigureAwait(false);
        return Answer(actId, pane);
    }

    private async ValueTask<Result<string>> RepoPath(StartSessionRequest request, CancellationToken cancellationToken)
    {
        var profile = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        var path = profile.IsSuccess ? profile.Value?.Profile.Repos?.FirstOrDefault(r => string.Equals(r.Name, request.Repo.Value, StringComparison.Ordinal))?.Path : null;
        if (path is null)
        {
            return SindriErrors.AgentRepoUnusable.With($"{request.Repo.Value} is not a repo of the profile");
        }

        return checks.FolderExists(path)
            ? path
            : SindriErrors.AgentRepoUnusable.With($"{request.Repo.Value}: the folder {path} is gone");
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
                : (OneOffWorktree?)made.Value;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return SindriErrors.AgentStartFailed.With("the worktree was not made within 60 s");
        }
    }

    /// <summary>A host failure: a name that already lives is a start that went through earlier (a replay after a restart), so it answers what was stored; any other failure is 014.</summary>
    private async ValueTask<Result<StartedSession>> Failed(Error error, string actId, CancellationToken cancellationToken)
    {
        if (error.Kind == ErrorKind.Conflict && await book.Find(actId, cancellationToken).ConfigureAwait(false) is { } stored)
        {
            return Answer(actId, stored.Pane);
        }

        return SindriErrors.AgentStartFailed.With(error.Message);
    }

    /// <summary>Removes what a failed start made: the session if it came up, then the worktree (git refuses a tree with changes; a fresh one has none).</summary>
    private async ValueTask Undo(OneOffWorktree? made, HostRef? started, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        if (started is not null)
        {
            _ = await host.Stop(started, CancellationToken.None).ConfigureAwait(false);
        }

        if (made is not null)
        {
            _ = await worktrees.Remove(made.Path, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async ValueTask Record(StartSessionRequest request, string actId, string pane, string? worktree, LeaseGrant grant)
    {
        var body = $"{request.Repo.Value}|{request.Agent.Value}|{request.Prompt}|{request.Worktree}";
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(body)));
        using var hold = await gate.Enter(CancellationToken.None).ConfigureAwait(false);
        if (hold is null)
        {
            return;   // another command held the gate for 5 s: the answer stays in memory, and the worktree route still finds it
        }

        var entry = events.Entry(EventTypes.AgentOp, new AgentOpRecorded("sindri-" + actId, "start", hash, pane, worktree), StartedSessions.Key(actId));
        _ = await ledger.Append(entry, grant, CancellationToken.None).ConfigureAwait(false);   // the session is running: a ledger that is down loses the durable copy, not the start
    }

    private StartedSession Answer(string actId, string pane) =>
        new(SessionId.From("sindri-" + actId), ActId.From(actId), "starting", new StartedHost(panes.Socket, "sindri-" + actId, pane));

    private static string InstallCommand() => OperatingSystem.IsMacOS() ? "brew install tmux" : "sudo apt install tmux";

    [GeneratedRegex("^[a-z0-9]{8,32}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ActIdShape { get; }
}
