using System.Collections.Concurrent;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;
using HostKind = Aw.Sindri.Core.Launch.HostKind;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>
/// The real <see cref="ILauncher"/> (API P10a, unit U7): catalog, host and viewer composed. It owns its stage bounds (host start
/// 4 s, then the viewer window), refuses what SL8 and the config gate refuse, and is idempotent per launch id: a second Launch
/// returns the first one's <see cref="StartedLaunch"/>.
/// </summary>
internal sealed partial class Launcher(LauncherParts parts) : ILauncher, ILaunchShutdown
{
    private static readonly TimeSpan _viewerGrace = TimeSpan.FromSeconds(1);
    private static readonly System.Text.Json.JsonSerializerOptions _captureJson = AwJson.CreateOptions(LauncherJsonContext.Default);
    private readonly ConcurrentDictionary<LaunchId, StartedLaunch> _started = new();
    private readonly LaunchGates _gates = new();

    /// <inheritdoc/>
    public async ValueTask<LaunchResult> Launch(LaunchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var turn = await _gates.Turn(request.Launch, cancellationToken).ConfigureAwait(false);
        if (_started.TryGetValue(request.Launch, out var existing))
        {
            return existing;
        }

        var result = await Start(request, cancellationToken).ConfigureAwait(false);
        if (result is StartedLaunch started)
        {
            _started[request.Launch] = started;
        }

        return result;
    }

    /// <inheritdoc/>
    public async ValueTask<Result<Unit>> Stop(LaunchId launch, CancellationToken cancellationToken)
    {
        using var turn = await _gates.Turn(launch, cancellationToken).ConfigureAwait(false);   // a Launch the caller cancelled finishes unwinding first, so it can't re-create the memory this removes
        _ = _started.TryRemove(launch, out _);
        var stopped = await parts.HostFor(false).Stop(new HostRef(HostKind.Tmux, SessionName(launch)), cancellationToken).ConfigureAwait(false);
        if (stopped.IsSuccess)
        {
            await parts.EndLive(launch, cancellationToken).ConfigureAwait(false);   // the session is over (a session tmux no longer has counts as stopped): the row stops being live now, and the lost sweep never marks it
        }

        await Cleanup(launch).ConfigureAwait(false);
        return stopped;
    }

    /// <inheritdoc/>
    public async ValueTask<int> StopAll(CancellationToken cancellationToken)
    {
        var ended = 0;
        foreach (var (launch, started) in _started.ToArray())
        {
            if (started.Host.Kind == HostKind.Tmux && (await Stop(launch, cancellationToken).ConfigureAwait(false)).IsSuccess)   // only sessions on Sindri's own tmux socket; a host that can't be asked leaves its row to the sweep
            {
                ended++;
            }
        }

        return ended;
    }

    /// <inheritdoc/>
    public async ValueTask<LaunchProbed> Probe(CancellationToken cancellationToken)
    {
        var terminal = await parts.Viewer.Probe(cancellationToken).ConfigureAwait(false);
        var host = await parts.HostFor(true).Probe(cancellationToken).ConfigureAwait(false);
        var targets = new List<TargetFacts>();
        foreach (var entry in parts.Catalog.All)
        {
            var found = await parts.ResolveBin(entry.Bin, cancellationToken).ConfigureAwait(false) is not null;
            targets.Add(new TargetFacts(entry.Id, found, Version: null, Limited(entry), found ? null : "not-installed", entry.Source, entry.Source == CatalogSource.Override));
        }

        return new LaunchProbed(terminal, host, targets, []);
    }

    /// <summary>Why a target works with limits: its entry names no usage reader (a budget on it is refused), or the reader is not yet checked against the real CLI (launcher L-4, API P10a Q11).</summary>
    private string? Limited(AgentEntry entry) =>
        entry.Usage is not { } name || string.Equals(name, "none", StringComparison.Ordinal) || parts.Usage(name) is not { } reader ? "no-usage-reader" : reader.Verified ? null : "usage-unverified";

    private static string SessionName(LaunchId launch) => LaunchSessionName.For(launch);

    private static FailedLaunch Fail(Error error) => new(error, LaunchStage.Host);

    private static Error ViewerError(ViewerOutcome outcome) =>
        string.Equals(outcome.Code, SindriErrors.LaunchTimedOut.Code, StringComparison.Ordinal)
            ? SindriErrors.LaunchTimedOut.With(outcome.Detail ?? "the viewer timed out")
            : SindriErrors.LaunchViewerFailed.With(outcome.Detail ?? "the viewer failed");

    private async ValueTask<LaunchResult> Start(LaunchRequest request, CancellationToken cancellationToken)
    {
        if (Refusal(request) is { } refused)
        {
            return Fail(refused);
        }

        var resolved = await Resolve(request, cancellationToken).ConfigureAwait(false);
        if (resolved.IsError)
        {
            return Fail(resolved.Error);
        }

        var entry = resolved.Value;
        var host = parts.HostFor(request.Attended);
        if (await Running(request, entry, host, cancellationToken).ConfigureAwait(false) is { } running)
        {
            return running;   // before Prepare can rewrite the hash the live wrap's heartbeats are checked against
        }

        try
        {
            var prepared = await Prepare(request, entry, cancellationToken).ConfigureAwait(false);
            if (prepared.IsError)
            {
                await Cleanup(request.Launch).ConfigureAwait(false);
                return Fail(prepared.Error);
            }

            return await Run(request, entry, host, prepared.Value, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _ = await host.Stop(new HostRef(host.Kind, SessionName(request.Launch)), CancellationToken.None).ConfigureAwait(false);   // the caller gave up: nobody will be told a session or its files exist
            await Cleanup(request.Launch).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>The launch's session when it is already running and this launch's files exist (a relaunch after a restart): answered as started, with nothing rewritten and nothing stopped.</summary>
    private async ValueTask<StartedLaunch?> Running(LaunchRequest request, AgentEntry entry, ISessionHost host, CancellationToken cancellationToken)
    {
        if (host.Kind == HostKind.None)
        {
            return null;
        }

        var session = new HostRef(host.Kind, SessionName(request.Launch));
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(parts.Options.HostDeadline);
        try
        {
            if (!await host.Exists(session, deadline.Token).ConfigureAwait(false) || !File.Exists(LaunchSecrets.PathFor(parts.Options.SecretsDirectory, request.Launch)))
            {
                return null;
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;   // the host didn't answer in time: the start that follows has its own bound
        }

        return new StartedLaunch(session, ViewerOutcome.Skipped, host.AttachArgv(session), entry.Source, entry.ArgvHash ?? string.Empty);
    }

    private Error? Refusal(LaunchRequest request)
    {
        if (request.Mode == LaunchMode.Attach)
        {
            return SindriErrors.LaunchNoSession.With("attach needs the launcher service's running session (fix: open the item from the app)");
        }

        if (parts.Config?.Invoke() is not { } config)
        {
            return null;
        }

        if (config.Unusable || (request.Attended && config.AttendedProblems.Count > 0))
        {
            return SindriErrors.LauncherInvalid.With((config.Problem ?? (config.AttendedProblems.Count > 0 ? config.AttendedProblems[0] : "launcher.json isn't valid")) + " (fix: sindri launch init --force, or delete the file to use the detected defaults)");
        }

        return config.Dropped.FirstOrDefault(d => string.Equals(d.Id, request.Agent.Value, StringComparison.Ordinal)) is { } dropped
            ? SindriErrors.LauncherInvalid.With($"{dropped.Id} is dropped from the catalog: {dropped.Reason}")
            : null;
    }

    private async ValueTask<Result<AgentEntry>> Resolve(LaunchRequest request, CancellationToken cancellationToken)
    {
        if (request.Attended)
        {
            return parts.Catalog.Resolve(request.Agent) is { } entry
                ? entry
                : SindriErrors.LaunchAgentMissing.With($"{request.Agent.Value} is not in the catalog (not-in-catalog; fix: run `sindri launch show`)");
        }

        var approved = await parts.Approved(cancellationToken).ConfigureAwait(false);
        return StepApproval.Resolve(parts.Catalog, request.Agent, parts.BuiltinHash(request.Agent), approved?.StepArgvHashes(request.Agent) ?? []);   // one entry: the check and the argv use the same one (SL8)
    }

    private async ValueTask<LaunchResult> Run(LaunchRequest request, AgentEntry entry, ISessionHost host, Prepared prepared, CancellationToken cancellationToken)
    {
        var name = SessionName(request.Launch);
        var wrapped = new List<string> { parts.Options.SindriExecutable, "session", "wrap", "--launch", request.Launch.Value, "--" };
        wrapped.AddRange(prepared.Argv);
        var source = (entry.Source, entry.ArgvHash ?? string.Empty);
        if (host.Kind == HostKind.None)
        {
            return await RunOnViewer(request, host, (name, prepared.WorkDir, wrapped), (source, prepared.Session), cancellationToken).ConfigureAwait(false);
        }

        var started = await host.Start(new HostSession(name, prepared.WorkDir, wrapped, prepared.Env), parts.Options.HostDeadline, cancellationToken).ConfigureAwait(false);
        if (started.IsError)
        {
            if (started.Error.Kind == ErrorKind.Conflict)
            {
                return Clash(request, entry, host, prepared, started.Error);   // the session is this launch's own, alive: never stopped, its files never deleted
            }

            _ = await host.Stop(new HostRef(host.Kind, name), CancellationToken.None).ConfigureAwait(false);   // a start that completes late must leave no orphan
            await Cleanup(request.Launch).ConfigureAwait(false);
            return Fail(started.Error);
        }

        cancellationToken.ThrowIfCancellationRequested();   // the host's start finished after the caller gave up
        ViewerOutcome viewer;
        try
        {
            viewer = request.Attended
                ? await OpenViewer(new ViewerRequest(started.Value, host.AttachArgv(started.Value), $"sindri {request.Item.Value}", parts.Options.ViewerWindow) { Cwd = prepared.WorkDir }, cancellationToken).ConfigureAwait(false)
                : ViewerOutcome.Skipped;
        }
        catch (OperationCanceledException)
        {
            _ = await host.Stop(started.Value, CancellationToken.None).ConfigureAwait(false);   // the caller gave up after the session started: nobody will be told it exists
            await Cleanup(request.Launch).ConfigureAwait(false);
            throw;
        }

        return new StartedLaunch(started.Value, viewer, host.AttachArgv(started.Value), source.Source, source.Item2) { ChosenSession = prepared.Session };
    }

    private async ValueTask<LaunchResult> RunOnViewer(LaunchRequest request, ISessionHost host, (string Name, string WorkDir, List<string> Argv) session, ((CatalogSource Source, string Hash) Entry, string? Chosen) started, CancellationToken cancellationToken)
    {
        var none = new HostRef(host.Kind, session.Name);
        var outcome = request.Attended
            ? await OpenViewer(new ViewerRequest(none, session.Argv, $"sindri {request.Item.Value}", parts.Options.ViewerWindow) { Cwd = session.WorkDir }, cancellationToken).ConfigureAwait(false)
            : ViewerOutcome.Skipped;
        if (outcome.Kind != ViewerOutcomeKind.Failed)
        {
            return new StartedLaunch(none, outcome, session.Argv, started.Entry.Source, started.Entry.Hash) { ChosenSession = started.Chosen };
        }

        _ = await host.Stop(none, CancellationToken.None).ConfigureAwait(false);   // a viewer that started a thread before failing
        await Cleanup(request.Launch).ConfigureAwait(false);
        return Fail(ViewerError(outcome));
    }

    private async ValueTask<ViewerOutcome> OpenViewer(ViewerRequest request, CancellationToken cancellationToken)
    {
        using var bound = new CancellationTokenSource(request.Window + _viewerGrace, parts.Time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, bound.Token);
        try
        {
            return await parts.Viewer.Open(request, linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ViewerOutcome.Failed(SindriErrors.LaunchTimedOut.Code, "the viewer did not return within its window");
        }
    }
}
