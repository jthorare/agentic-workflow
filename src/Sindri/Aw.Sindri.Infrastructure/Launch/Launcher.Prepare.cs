using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launch.Argv;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>The launch's files and argv (the part of <see cref="Launcher"/> that prepares a start and cleans up after one).</summary>
internal sealed partial class Launcher
{
    private async ValueTask<Result<Prepared>> Prepare(LaunchRequest request, AgentEntry entry, CancellationToken cancellationToken)
    {
        var created = request.Worktree is { } expected && !Directory.Exists(expected);
        var located = await WorkDirectory(request, cancellationToken).ConfigureAwait(false);
        if (located.IsError)
        {
            return located.Error;
        }
        var workDir = located.Value;
        if (await GuardAndPrepare(request, entry, (workDir, created), cancellationToken).ConfigureAwait(false) is { } refusedStart)
        {
            return refusedStart;   // the guard's refusal, or SND-LAUNCH-021 (a step failure; nothing started)
        }

        try
        {
            if (await WriteStepFiles(request, workDir, cancellationToken).ConfigureAwait(false) is { } refused)
            {
                return refused;
            }

            var composed = await Compose(request, entry, workDir, cancellationToken).ConfigureAwait(false);
            if (composed.IsError)
            {
                return composed.Error;
            }

            WriteCaptureRule(request.Launch, entry);
            var prior = await MintSecret(request.Launch, cancellationToken).ConfigureAwait(false);
            var confined = await Confine(request, composed.Value.Argv, Environment(request, workDir), composed.Value.Session, cancellationToken).ConfigureAwait(false);
            if (confined.IsError)
            {
                return confined.Error;   // a refusal carries its SND-LAUNCH-014..017 or -020 code; Start cleans up (and releases) after it
            }

            return new Prepared(workDir, confined.Value.Argv, confined.Value.Env, composed.Value.Session?.Value) { PriorHash = prior, Seed = composed.Value.Seed, Sandbox = confined.Value.Summary, SandboxRunArea = confined.Value.RunArea };
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return KernelErrors.Unavailable.With($"the launch's files could not be written: {e.Message}");
        }
    }

    /// <summary>Mints the launch's secret and writes its hash; returns the hash file as it was before (a live session of this launch may own it), or null when there was none.</summary>
    private async ValueTask<byte[]?> MintSecret(LaunchId launch, CancellationToken cancellationToken)
    {
        var secret = LaunchSecrets.Mint();
        var hashFile = LaunchSecrets.PathFor(parts.Options.SecretsDirectory, launch);
        var prior = File.Exists(hashFile) ? await File.ReadAllBytesAsync(hashFile, cancellationToken).ConfigureAwait(false) : null;
        LaunchSecrets.Write(parts.Options.SecretsDirectory, launch, secret);
        return prior;
    }

    /// <summary>LN-WF5: the Step's pack, in its directory before the session starts (an attended launch has none); an error when the pack is refused.</summary>
    private Error? WriteBundle(LaunchRequest request, string workDir) =>
        request is { Attended: false, Step.Bundle: { } bundle }
            ? PackBundleFiles.Write(StepEnvironment.For(request, parts.Options.SindriHome).GetValueOrDefault(StepEnvironment.RunDirectory, workDir), bundle)
            : null;

    /// <summary>The Step's pack and its declared read facts, in its run directory before the session starts; the first refusal, else null.</summary>
    private async ValueTask<Error?> WriteStepFiles(LaunchRequest request, string workDir, CancellationToken cancellationToken)
    {
        MakeRunDirectory(request);
        return WriteBundle(request, workDir) ?? await WriteFacts(request, workDir, cancellationToken).ConfigureAwait(false) ?? await WriteOutputFolder(request, workDir, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>SB17: the Step's own run directory under Sindri's home (0700), made before its files and before the sandbox checks that it exists; nothing without a home (the run directory is then the worktree).</summary>
    private void MakeRunDirectory(LaunchRequest request)
    {
        var directory = request.Attended
            ? parts.Sandbox is null ? null : AttendedOpen.RunDirectory(parts.Options.SindriHome, request.Launch)   // why: the sandbox plan keeps its files here (sandbox spec 3.9)
            : parts.Options.SindriHome is null ? null : StepEnvironment.RunDirectoryOf(request, parts.Options.SindriHome);
        if (directory is not null)
        {
            OwnerOnly.EnsureDirectory(directory);
        }
    }

    /// <summary>The folder the Step's outputs go to, made and excluded from git before the session starts (an attended launch has no Step and no outputs).</summary>
    private async ValueTask<Error?> WriteOutputFolder(LaunchRequest request, string workDir, CancellationToken cancellationToken) =>
        request is { Attended: false, Step: not null }
            ? await StepFactWriter.Outputs(StepEnvironment.For(request, parts.Options.SindriHome).GetValueOrDefault(StepEnvironment.RunDirectory, workDir), cancellationToken).ConfigureAwait(false)
            : null;

    /// <summary>The Step's declared read facts, as files in its run directory before the session starts (an attended launch has none, as it has no bundle: a human open has no Step directory and no brief to name the files); an error when a name is refused.</summary>
    private async ValueTask<Error?> WriteFacts(LaunchRequest request, string workDir, CancellationToken cancellationToken) =>
        request is { Attended: false, Step.Facts: { Count: > 0 } facts }
            ? await StepFactWriter.Write(StepEnvironment.For(request, parts.Options.SindriHome).GetValueOrDefault(StepEnvironment.RunDirectory, workDir), facts, cancellationToken).ConfigureAwait(false)
            : null;

    /// <summary>Where the session runs: the holdout evaluator's own scratch checkout at the head sha (removed with the launch, LN-LCH2), else the launch's worktree or the repo.</summary>
    private async ValueTask<Result<string>> WorkDirectory(LaunchRequest request, CancellationToken cancellationToken)
    {
        if (request.Evaluator is not { } evaluator)
        {
            return await LaunchWorktree.Ensure(request, cancellationToken).ConfigureAwait(false) is { } missing ? missing : request.Worktree ?? request.RepoPath;
        }

        if (request.Attended || request.Step?.Policy is null)
        {
            return SindriErrors.RequestInvalid.With("the holdout evaluator launches unattended and with a policy (fix: build the request with AgentLaunches.EvaluatorRequestFor)");
        }

        return await parts.Scratch.Create(request.RepoPath, ScratchPath(request.Launch), evaluator.HeadSha, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<(IReadOnlyList<string> Argv, SessionId? Session, string Seed)>> Compose(LaunchRequest request, AgentEntry entry, string workDir, CancellationToken cancellationToken)
    {
        var step = Denying(request.Attended ? null : request.Step, request, entry, workDir);
        var policyPath = step?.Policy is { } policy && entry.PolicyForm == PolicyForm.ClaudeSettingsFile
            ? StepPolicyFile.Write(parts.Options.PolicyDirectory, request.Launch, policy)
            : null;
        var session = request.Mode == LaunchMode.Resume && request.Resume is { } resumed ? resumed : PreChosen(entry);
        var seed = ArgvBuilder.Seed(request.Item) + StepSeed(step);
        var (mcpPath, mcpArguments) = McpLaunchConfig.Prepare(entry.McpForm, entry.McpForm == McpForm.None ? null : ForStep(await parts.McpFor(request.RepoPath, cancellationToken).ConfigureAwait(false), request), parts.Options.LaunchDirectory, request.Launch);
        var built = ArgvBuilder.Build(entry, request.Mode, new ArgvInputs(request.Item, workDir, SessionName(request.Launch), seed, session, mcpPath, step, policyPath));
        if (built.IsError)
        {
            return built.Error;
        }

        IReadOnlyList<string> argv = [.. built.Value, .. mcpArguments];
        return (argv, session, seed);
    }

    /// <summary>The Step with its denied paths added: the other worktrees and the holdout store. Only a form that can express a denied read gets them, since a codex entry would refuse the whole Step with SND-LAUNCH-013.</summary>
    private StepArgs? Denying(StepArgs? step, LaunchRequest request, AgentEntry entry, string workDir)
    {
        if (step?.Policy is not { } stated)
        {
            return step;
        }

        if (request.Evaluator is not null)
        {
            // The evaluator's scratch checkout is the one path it writes, and its set is the one holdout path it reads: the store is not denied wholesale (no
            // Sindri home passed), and its policy already names the phase worktree and the set. Another form is refused by the argv builder (SND-LAUNCH-013).
            return step with { Policy = stated with { WritablePath = workDir } };
        }

        if (entry.PolicyForm != PolicyForm.ClaudeSettingsFile)
        {
            return step;
        }

        var siblings = request.Worktree is null ? [] : SiblingWorktrees(workDir);
        return step with { Policy = stated with { DeniedPaths = [.. stated.DeniedPaths ?? [], .. StepDenials.For(workDir, siblings, parts.Options.SindriHome)] } };
    }

    /// <summary>The Sindri worktrees beside <paramref name="workDir"/> ('sindri-*' under the same parent), which a Step must not read.</summary>
    private static string[] SiblingWorktrees(string workDir)
    {
        var parent = Path.GetDirectoryName(workDir.TrimEnd('/', '\\'));
        try
        {
            return parent is not null && Directory.Exists(parent) ? [.. Directory.EnumerateDirectories(parent, "sindri-*")] : [];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];   // nothing readable beside it to name
        }
    }

    /// <summary>What an unattended launch adds to the seed: which Step it is, then the Step's brief when it has one (engine P8 F13).</summary>
    private static string StepSeed(StepArgs? step) =>
        step is null ? string.Empty : $" This is the {step.Ref.State} step, block {step.Ref.Block}." + (step.Brief is { Length: > 0 } brief ? " " + brief : string.Empty);

    /// <summary>The session id an entry in 'pre-chosen-uuid' mode is told to use, else none (an id reported later is recorded then, launcher L-9).</summary>
    // An agent Step's launch (the request has Step arguments, an instance and a run) tells the endpoint which instance and run it is; a plain session keeps the repo alone.
    private static McpTarget? ForStep(McpTarget? target, LaunchRequest request) =>
        target is not null && request.Step is not null && request.Instance is { } instance && request.Run is { } run ? target with { Instance = instance.Value, Run = run.Value } : target;

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

    private Dictionary<string, string> Environment(LaunchRequest request, string workDir)
    {
        var env = new Dictionary<string, string>(StepEnvironment.For(request, parts.Options.SindriHome), StringComparer.Ordinal)
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

        if (request.Attended || request.Step is null)
        {
            return env;
        }

        var ghConfig = GhConfigPath(request.Launch);
        _ = Directory.CreateDirectory(ghConfig);   // empty: the Step's gh has no login to find
        return new Dictionary<string, string>(LaunchEnvironment.Scrub(env, workDir, ghConfig), StringComparer.Ordinal);
    }

    private string GhConfigPath(LaunchId launch) => Path.Combine(parts.Options.LaunchDirectory, launch.Value + ".gh");

    private async ValueTask Cleanup(LaunchId launch)
    {
        try
        {
            Directory.Delete(GhConfigPath(launch), recursive: true);   // a missing one is fine
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // an empty directory left behind holds nothing
        }

        LaunchSecrets.Delete(parts.Options.SecretsDirectory, launch);
        LaunchFileStore.DeleteFlat(parts.Options, launch);
        LaunchFileStore.DeleteIn(parts.Options.PolicyDirectory, StepPolicyFile.PathFor(parts.Options.PolicyDirectory, launch));
        LaunchFileStore.DeleteIn(parts.Options.LaunchDirectory, McpLaunchConfig.PathFor(parts.Options.LaunchDirectory, launch));
        _ = await parts.Scratch.Remove(ScratchPath(launch), CancellationToken.None).ConfigureAwait(false);   // the evaluator's checkout; none for any other launch
        await ReleaseSandbox(launch).ConfigureAwait(false);
    }

    private string ScratchPath(LaunchId launch) => Path.Combine(parts.Options.ScratchDirectory, "eval-" + launch.Value);

    private sealed record Prepared(string WorkDir, IReadOnlyList<string> Argv, Dictionary<string, string> Env, string? Session)
    {
        /// <summary>The hash file as it was before this start rewrote it, or null when there was none.</summary>
        public byte[]? PriorHash { get; init; }

        /// <summary>The first message, for a host that runs the agent itself (T3) rather than the argv.</summary>
        public string? Seed { get; init; }

        /// <summary>The sandbox summary of a confined Step launch (<c lang="text">launch.started</c>'s sandbox field), or null when it ran unconfined.</summary>
        public SandboxSummary? Sandbox { get; init; }

        /// <summary>The confined launch's run directory, where <c lang="text">wrap-limits.json</c> sits, or null when it ran unconfined.</summary>
        public string? SandboxRunArea { get; init; }
    }

    /// <summary>The start clashed with a session of this launch's name: it is the launch's own and stays untouched. The hash the live wrap checks is put back (and the session answered as started), or, with no hash to put back, the minted pair is removed and the launch fails.</summary>
    private LaunchResult Clash(LaunchRequest request, AgentEntry entry, ISessionHost host, Prepared prepared, Error clash)
    {
        LaunchSecrets.Delete(parts.Options.SecretsDirectory, request.Launch);
        if (prepared.PriorHash is not { } prior)
        {
            return Fail(clash);
        }

        OwnerOnly.WriteAtomically(LaunchSecrets.PathFor(parts.Options.SecretsDirectory, request.Launch), prior);
        var session = new HostRef(host.Kind, SessionName(request.Launch));
        return new StartedLaunch(session, ViewerOutcome.Skipped, host.AttachArgv(session), entry.Source, entry.ArgvHash ?? string.Empty);
    }
}
