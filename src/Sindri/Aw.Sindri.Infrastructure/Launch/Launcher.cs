using System.Collections.Concurrent;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launch.Argv;
using Aw.Sindri.Core.Workflow.Engine;
using HostKind = Aw.Sindri.Core.Launch.HostKind;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>
/// The real <see cref="ILauncher"/> (API P10a, unit U7): catalog, host and viewer composed. It owns its stage bounds (host start
/// 4 s, then the viewer window), refuses what SL8 and the config gate refuse, and is idempotent per launch id: a second Launch
/// returns the first one's <see cref="StartedLaunch"/>.
/// </summary>
internal sealed class Launcher(LauncherParts parts) : ILauncher
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
        _ = _started.TryRemove(launch, out _);
        var stopped = await parts.HostFor(false).Stop(new HostRef(HostKind.Tmux, SessionName(launch)), cancellationToken).ConfigureAwait(false);
        Cleanup(launch);
        return stopped;
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
        var prepared = await Prepare(request, entry, cancellationToken).ConfigureAwait(false);
        if (prepared.IsError)
        {
            Cleanup(request.Launch);
            return Fail(prepared.Error);
        }

        return await Run(request, entry, host, prepared.Value, cancellationToken).ConfigureAwait(false);
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

    private async ValueTask<Result<Prepared>> Prepare(LaunchRequest request, AgentEntry entry, CancellationToken cancellationToken)
    {
        if (await LaunchWorktree.Ensure(request, cancellationToken).ConfigureAwait(false) is { } missing)
        {
            return missing;
        }

        var workDir = request.Worktree ?? request.RepoPath;
        try
        {
            var composed = await Compose(request, entry, workDir, cancellationToken).ConfigureAwait(false);
            if (composed.IsError)
            {
                return composed.Error;
            }

            WriteCaptureRule(request.Launch, entry);
            var secret = LaunchSecrets.Mint();
            LaunchSecrets.Write(parts.Options.SecretsDirectory, request.Launch, secret);
            return new Prepared(workDir, composed.Value.Argv, Environment(request), composed.Value.Session?.Value);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return KernelErrors.Unavailable.With($"the launch's files could not be written: {e.Message}");
        }
    }

    private async ValueTask<Result<(IReadOnlyList<string> Argv, SessionId? Session)>> Compose(LaunchRequest request, AgentEntry entry, string workDir, CancellationToken cancellationToken)
    {
        var step = request.Attended ? null : request.Step;
        var policyPath = step?.Policy is { } policy && entry.PolicyForm == PolicyForm.ClaudeSettingsFile
            ? StepPolicyFile.Write(parts.Options.PolicyDirectory, request.Launch, policy)
            : null;
        var session = request.Mode == LaunchMode.Resume && request.Resume is { } resumed ? resumed : PreChosen(entry);
        var seed = ArgvBuilder.Seed(request.Item) + (step is null ? string.Empty : $" This is the {step.Ref.State} step, block {step.Ref.Block}.");
        var (mcpPath, mcpArguments) = McpLaunchConfig.Prepare(entry.McpForm, entry.McpForm == McpForm.None ? null : await parts.McpFor(request.RepoPath, cancellationToken).ConfigureAwait(false), parts.Options.LaunchDirectory, request.Launch);
        var built = ArgvBuilder.Build(entry, request.Mode, new ArgvInputs(request.Item, workDir, SessionName(request.Launch), seed, session, mcpPath, step, policyPath));
        if (built.IsError)
        {
            return built.Error;
        }

        IReadOnlyList<string> argv = [.. built.Value, .. mcpArguments];
        return (argv, session);
    }

    /// <summary>The session id an entry in 'pre-chosen-uuid' mode is told to use, else none (an id reported later is recorded then, launcher L-9).</summary>
    private static SessionId? PreChosen(AgentEntry entry) => entry.SessionIdMode == SessionIdMode.PreChosenUuid ? SessionId.From(Guid.NewGuid().ToString("D")) : null;

    /// <summary>The capture rule 'session wrap' reads for an entry whose CLI reports its own id (launcher L-9): 0600, no secret in it.</summary>
    private void WriteCaptureRule(LaunchId launch, AgentEntry entry)
    {
        if (entry.SessionIdMode != SessionIdMode.Reported)
        {
            return;
        }

        OwnerOnly.EnsureDirectory(parts.Options.LaunchesDirectory);
        var rule = new CaptureRule(entry.SessionIdPattern, entry.ReportedNewestFile, entry.IdCapture);
        OwnerOnly.WriteAtomically(Path.Combine(parts.Options.LaunchesDirectory, LaunchFiles.CaptureName(launch)), AwJson.Serialize(rule, _captureJson));
    }

    private Dictionary<string, string> Environment(LaunchRequest request)
    {
        var env = new Dictionary<string, string>(StepEnvironment.For(request), StringComparer.Ordinal)
        {
            ["AW_SINDRI_CHILD"] = "1",
            ["AW_SINDRI_ITEM"] = request.Item.Value,
            ["AW_SINDRI_LAUNCH"] = request.Launch.Value,
            ["AW_SINDRI_LAUNCH_SECRET_FILE"] = LaunchSecrets.SecretPathFor(parts.Options.SecretsDirectory, request.Launch),   // the path only: the secret itself never sits in a tmux command line
        };
        if (parts.Options.SindriHome is { } home)
        {
            env["AW_SINDRI_HOME"] = home;
        }

        return env;
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
            _ = await host.Stop(new HostRef(host.Kind, name), CancellationToken.None).ConfigureAwait(false);   // a start that completes late must leave no orphan
            Cleanup(request.Launch);
            return Fail(started.Error);
        }

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
            Cleanup(request.Launch);
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
        Cleanup(request.Launch);
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

    private void Cleanup(LaunchId launch)
    {
        LaunchSecrets.Delete(parts.Options.SecretsDirectory, launch);
        LaunchFileStore.DeleteFlat(parts.Options, launch);
        LaunchFileStore.DeleteIn(parts.Options.PolicyDirectory, StepPolicyFile.PathFor(parts.Options.PolicyDirectory, launch));
        LaunchFileStore.DeleteIn(parts.Options.LaunchDirectory, McpLaunchConfig.PathFor(parts.Options.LaunchDirectory, launch));
    }

    private sealed record Prepared(string WorkDir, IReadOnlyList<string> Argv, Dictionary<string, string> Env, string? Session);
}
