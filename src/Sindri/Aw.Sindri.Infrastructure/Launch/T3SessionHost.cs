using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>
/// The 't3' host (launcher.json <c lang="text">"host": { "kind": "t3" }</c>): a human open starts a T3 Code thread in the item's worktree instead of a tmux
/// session, and the T3 app shows it. T3 runs the agent itself, so the session's argv and environment are not used and no 'session wrap' runs; the host
/// reports its sessions' liveness instead (<see cref="IHostedSessions"/>). The thread uses the T3 project's default provider and model. Steps never use it.
/// </summary>
/// <param name="api">T3 Code's tools.</param>
/// <param name="map">The sessions it started.</param>
/// <param name="options">The link opener.</param>
/// <param name="repos">What git says a repo folder is, for finding its project.</param>
internal sealed class T3SessionHost(IT3Api api, T3SessionMap map, T3Options options, IT3RepoIdentities repos) : ISessionHost, IHostedSessions
{
    /// <inheritdoc/>
    public HostKind Kind => HostKind.T3;

    /// <inheritdoc/>
    /// <remarks>Each T3 call has its own budget (<see cref="T3Options.CallBudget"/>); the 4 s start deadline bounds a tmux process start and isn't applied here.</remarks>
    public async ValueTask<Result<HostRef>> Start(HostSession session, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (await Refusal(session.Name, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        var environment = await api.Environment(cancellationToken).ConfigureAwait(false);
        if (environment.IsError)
        {
            return environment.Error;
        }

        var repo = session.RepoPath ?? session.WorkDir;
        var project = await ProjectFor(repo, cancellationToken).ConfigureAwait(false);
        if (project.IsError)
        {
            return project.Error;
        }

        var model = await ModelFor(session.Agent, cancellationToken).ConfigureAwait(false);
        if (model.IsError)
        {
            return model.Error;
        }

        var worktree = T3Projects.Same(session.WorkDir, project.Value.Root) ? null : session.WorkDir;   // any other folder (a worktree, or a clone that isn't the project's own) is bound explicitly
        var request = new T3LaunchRequest(project.Value.Id, session.Title ?? session.Name, session.Seed ?? $"Sindri session {session.Name}.", worktree, model.Value);
        var thread = await api.Launch(request, cancellationToken).ConfigureAwait(false);
        if (thread.IsError)
        {
            return thread.Error;
        }

        map.Save(new T3SessionEntry(session.Name, thread.Value, environment.Value.Id));
        return new HostRef(HostKind.T3, session.Name);
    }

    /// <inheritdoc/>
    /// <remarks>Stops the thread's running turn; the thread itself stays in T3, where the user can go on with it.</remarks>
    public async ValueTask<Result<Unit>> Stop(HostRef host, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (map.Get(host.Name) is not { } entry)
        {
            return Unit.Value;
        }

        var stopped = await api.Interrupt(entry.ThreadId, cancellationToken).ConfigureAwait(false);
        if (stopped.IsSuccess)
        {
            map.Remove(host.Name);
        }

        return stopped;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> Exists(HostRef host, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (map.Get(host.Name) is not { } entry)
        {
            return false;
        }

        var read = await api.Read(entry.ThreadId, cancellationToken).ConfigureAwait(false);
        return read is { IsSuccess: true, Value.Archived: false };
    }

    /// <inheritdoc/>
    /// <remarks>T3 has no attached-client count; the app shows every thread.</remarks>
    public ValueTask<int> AttachedClients(HostRef host, CancellationToken cancellationToken) => ValueTask.FromResult(0);

    /// <inheritdoc/>
    /// <remarks>Opens the thread in the T3 app through its 't3code://threads/&lt;environment&gt;/&lt;thread&gt;' link; empty for a session this host doesn't know.</remarks>
    public IReadOnlyList<string> AttachArgv(HostRef host)
    {
        ArgumentNullException.ThrowIfNull(host);
        return map.Get(host.Name) is { } entry ? [options.Opener, Link(entry)] : [];
    }

    /// <inheritdoc/>
    public async ValueTask<HostFacts> Probe(CancellationToken cancellationToken)
    {
        var environment = await api.Environment(cancellationToken).ConfigureAwait(false);
        return environment.IsSuccess
            ? new HostFacts(HostKind.T3, Present: true, environment.Value.Version, Problem: null)
            : new HostFacts(HostKind.T3, Present: false, Version: null, Problem: "not-running");
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<HostedSession>> Check(CancellationToken cancellationToken)
    {
        var sessions = new List<HostedSession>();
        foreach (var entry in map.All())
        {
            if (Launch(entry.Name) is { } launch)
            {
                sessions.Add(new HostedSession(launch, await Alive(entry, cancellationToken).ConfigureAwait(false)));
            }
        }

        return sessions;
    }

    /// <inheritdoc/>
    public void Forget(LaunchId launch) => map.Remove(LaunchSessionName.For(launch));

    /// <summary>Whether the session is called <paramref name="name"/> and this host started it.</summary>
    /// <param name="name">The session name.</param>
    /// <returns>True when the map has it.</returns>
    public bool Owns(string name) => map.Get(name) is not null;

    /// <summary>
    /// Why a start of <paramref name="name"/> can't go ahead, checked before T3 is asked: a name the map can't hold (a thread nobody could find again is
    /// never started), or a live thread under that name already.
    /// </summary>
    private async ValueTask<Error?> Refusal(string name, CancellationToken cancellationToken)
    {
        if (!map.Holds(name))
        {
            return KernelErrors.Invalid.With($"'{name}' isn't a session name");
        }

        return map.Get(name) is { } existing && await Alive(existing, cancellationToken).ConfigureAwait(false)
            ? KernelErrors.Conflict.With($"{name} is already a T3 thread ({existing.ThreadId})")
            : null;
    }

    /// <summary>
    /// The T3 provider and model for the launch's agent: the first enabled instance of its driver (claude-code runs on T3's Claude, codex on Codex, cursor
    /// on Cursor), or SND-LAUNCH-027 when T3 has none enabled. An agent T3 has no driver for (an added entry), or none given, runs on the project's default.
    /// </summary>
    private async ValueTask<Result<T3Model?>> ModelFor(AgentId? agent, CancellationToken cancellationToken)
    {
        if (agent is not { } id || T3Providers.DriverFor(id.Value) is not { } driver)
        {
            return (T3Model?)null;
        }

        var model = await api.ModelFor(driver, cancellationToken).ConfigureAwait(false);
        if (model.IsError)
        {
            return model.Error;
        }

        return model.Value is { } found
            ? found
            : SindriErrors.LaunchT3NoProvider.With($"T3 Code has no enabled {driver} provider for {id.Value} (fix: enable it in T3 Code's settings, or open with --agent <another agent>)");
    }

    /// <summary>The T3 project <paramref name="repo"/> belongs to (<see cref="T3Projects.Resolve"/>), or SND-LAUNCH-026 with the command that adds it.</summary>
    private async ValueTask<Result<T3Project>> ProjectFor(string repo, CancellationToken cancellationToken)
    {
        var projects = await api.Projects(cancellationToken).ConfigureAwait(false);
        if (projects.IsError)
        {
            return projects.Error;
        }

        var identity = await repos.Of(repo, cancellationToken).ConfigureAwait(false);
        return T3Projects.Resolve(projects.Value, repo, identity) is { } project
            ? project
            : SindriErrors.LaunchT3NoProject.With($"no T3 Code project is {repo} or the same repository (fix: t3 project add {repo})");
    }

    /// <summary>Alive unless T3 says the thread is gone or archived: when T3 can't be asked (closed, restarting for an update) the thread is still there.</summary>
    private async ValueTask<bool> Alive(T3SessionEntry entry, CancellationToken cancellationToken)
    {
        var read = await api.Read(entry.ThreadId, cancellationToken).ConfigureAwait(false);
        return read.IsError || read.Value is { Archived: false };
    }

    private static string Link(T3SessionEntry entry) => $"t3code://threads/{entry.EnvironmentId}/{entry.ThreadId}";

    private static LaunchId? Launch(string name) =>
        name.StartsWith("snd-", StringComparison.Ordinal) && LaunchId.TryFrom(name["snd-".Length..], out var launch) ? launch : null;

}
