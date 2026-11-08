using System.Collections.Concurrent;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Infrastructure.Launch;

namespace Aw.Sindri.Infrastructure.Sandbox.Launch;

/// <summary>
/// The launcher's sandbox step (sandbox spec §12 SB-5a) for one unattended launch: probe, then, under the session cap, request, plan, session home, egress, self-check and wrap,
/// inside a systemd scope. A failure after the plan discards what the wrap left and releases the launch's parts; <see cref="Release"/> does the same at the end of a session.
/// </summary>
internal sealed class SandboxLaunch(ISandbox sandbox, ISessionCap cap, ISandboxLaunchStages stages, SandboxLaunchOptions options, LaunchSecretRegistry secrets) : ISandboxLaunch
{
    private static readonly TimeSpan _fallbackLongMax = TimeSpan.FromHours(2);   // workflow.steps.longMax's default when no profile is approved
    private const string LaunchSecretVariable = "AW_SINDRI_LAUNCH_SECRET";
    private readonly ConcurrentDictionary<LaunchId, SandboxPlan> _plans = new();

    /// <inheritdoc/>
    public async ValueTask<Result<PreparedSandbox>> Prepare(LaunchRequest request, IReadOnlyList<string> argv, SessionId? chosenSession, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(argv);
        var probed = sandbox.Probe();
        if (probed.IsError)
        {
            return probed.Error;
        }

        var asked = await stages.Request(request, chosenSession, cancellationToken).ConfigureAwait(false);
        if (asked.IsError)
        {
            return asked.Error;
        }

        var limit = asked.Value.Settings?.Limits?.Sessions ?? SandboxDefaults.Sessions;
        var prepared = await cap.Start(limit, ct => Confine(asked.Value, argv, ct), cancellationToken).ConfigureAwait(false);
        if (prepared.IsError && request.Attended && string.Equals(prepared.Error.Code, SindriErrors.LaunchSessionWait.Code, StringComparison.Ordinal))
        {
            prepared = AttendedFull(prepared.Error);   // a person can close a session: said at once, not a wait
        }

        if (prepared.IsError)
        {
            await Undo(request.Launch, CancellationToken.None).ConfigureAwait(false);
        }

        return prepared;
    }

    private static Error AttendedFull(Error full)
    {
        var values = SandboxBlocker.Of(full).Values;
        var text = SandboxCopy.For(full.Code, SandboxReasons.Sessions, source: null, values, attended: true)?.Text ?? full.Message;
        return SandboxRefusal.For(SindriErrors.LaunchSessionWait, SandboxReasons.Sessions, text, details: values);
    }

    /// <inheritdoc/>
    public ValueTask Release(LaunchId launch, CancellationToken cancellationToken) => Undo(launch, cancellationToken);

    private async Task<Result<PreparedSandbox>> Confine(SandboxRequest request, IReadOnlyList<string> argv, CancellationToken cancellationToken)
    {
        var planned = sandbox.Plan(request);
        if (planned.IsError)
        {
            return planned.Error;
        }

        var plan = planned.Value;
        _plans[plan.Launch] = plan;
        var staged = await Stage(request, plan, cancellationToken).ConfigureAwait(false);
        if (staged.IsError)
        {
            return staged.Error;
        }

        var checkedPlan = await sandbox.SelfCheck(plan, cancellationToken).ConfigureAwait(false);
        if (checkedPlan.IsError)
        {
            return checkedPlan.Error;
        }

        var wrapped = Wrap(plan, staged.Value.Policy.Apply(argv));   // the policy file now sits in the session home; a Codex Step's -c overrides go after the launcher's own
        if (wrapped.IsError)
        {
            return wrapped.Error;
        }

        var limits = ScopeLimits.From(request.Settings?.Limits, options.Box, request.StepTimeout, LongMaxOf(request), request.Attended);
        var written = WriteWrapLimits(plan, limits);
        if (written.IsError)
        {
            return written.Error;
        }

        var scoped = options.Scope is { } scope ? scope(plan.Launch, limits, wrapped.Value) : SystemdScopeArgv.Start(options.SystemdRun, plan.Launch, limits, wrapped.Value);
        return new PreparedSandbox(plan.ToSummary(), scoped, staged.Value.Home, plan.RunDirectory);
    }

    // #2151: a path on the agent's command line that the sandbox hides would start the agent on a missing file, so the launch is refused before it starts.
    private Result<IReadOnlyList<string>> Wrap(SandboxPlan plan, IReadOnlyList<string> inside) =>
        PlanArgvPaths.Check(plan, inside) is { } hidden ? hidden : sandbox.Wrap(plan, inside);

    private string UnitOf(LaunchId launch) => options.UnitFor is { } unit ? unit(launch) : SystemdScopeArgv.UnitFor(launch);

    /// <summary>Writes the scope's limits where 'session wrap --limits' reads them; a sandboxed agent must never run unwatched, so a failed write refuses the start (#1553).</summary>
    private Result<Unit> WriteWrapLimits(SandboxPlan plan, ScopeLimits limits)
    {
        var file = new WrapLimitsFile(
            UnitOf(plan.Launch),
            (long)limits.WallGrace.TotalSeconds,
            (long)limits.ParkMax.TotalSeconds,
            limits.MemoryMaxBytes,
            limits.Wall is { } wall ? (long)wall.TotalSeconds : null);
        try
        {
            OwnerOnly.WriteAtomically(Path.Combine(plan.RunDirectory, LaunchPaths.WrapLimitsName), AwJson.Serialize(file, SandboxFilesJson.Options));
            return Unit.Value;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return SandboxRefusal.For(SindriErrors.LaunchSelfCheckFailed, SandboxReasons.FsWrite, $"the scope's limits file could not be written: {e.Message}");
        }
    }

    /// <summary>The host side before the self-check: the session home, the Step's policy files, and the egress.</summary>
    private async ValueTask<Result<Staged>> Stage(SandboxRequest request, SandboxPlan plan, CancellationToken cancellationToken)
    {
        var home = MakeHome(plan);
        if (home.IsError)
        {
            return home.Error;
        }

        var mcp = MoveMcpConfig(plan, home.Value);
        if (mcp.IsError)
        {
            return mcp.Error;
        }

        var policy = await stages.WritePolicy(request, plan, home.Value, cancellationToken).ConfigureAwait(false);
        if (policy.IsError)
        {
            return policy.Error;
        }

        var egress = await stages.StartEgress(plan, cancellationToken).ConfigureAwait(false);
        return egress.IsError ? egress.Error : new Staged(home.Value, policy.Value);
    }

    private sealed record Staged(string Home, PolicyFiles Policy);

    /// <summary>Makes the session home with the launch's one-read secret moved in (the launcher's own file is deleted: the wrap reads the home's) and control.json as a file, which a sandbox needs.</summary>
    private Result<string> MakeHome(SandboxPlan plan)
    {
        if (options.Control() is not { } control)
        {
            return SandboxRefusal.For(SindriErrors.LaunchSelfCheckFailed, SandboxReasons.NetForward, "the control plane's endpoint is not known");
        }

        var file = LaunchSecrets.SecretPathFor(options.LaunchSecretsDirectory, plan.Launch);
        try
        {
            var secret = File.ReadAllText(file);
            var home = SessionHome.Create(options.SindriHome, plan.Launch, secret, control);
            File.Delete(file);
            secrets.Put(plan.Launch, new Dictionary<string, string>(StringComparer.Ordinal) { [LaunchSecretVariable] = secret });   // #2163: before the wrap, which hands it to the agent on descriptor 3 only; Undo forgets it
            return home;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return SandboxRefusal.For(SindriErrors.LaunchSelfCheckFailed, SandboxReasons.FsWrite, "the session home could not be made: " + e.Message);
        }
    }

    // #2151: the launcher staged the MCP config under launch/ (hidden by the Sindri-home mask) and gave the agent its place in the session home; a launch without one has no staged file.
    private Result<Unit> MoveMcpConfig(SandboxPlan plan, string sessionHome)
    {
        var from = McpLaunchConfig.PathFor(options.LaunchDirectory, plan.Launch);
        try
        {
            if (File.Exists(from))
            {
                File.Move(from, McpLaunchConfig.PathFor(sessionHome, plan.Launch), overwrite: true);
            }

            return Unit.Value;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return SandboxRefusal.For(SindriErrors.LaunchSelfCheckFailed, SandboxReasons.FsWrite, "the MCP config could not be moved into the session home: " + e.Message);
        }
    }

    private async ValueTask Undo(LaunchId launch, CancellationToken cancellationToken)
    {
        secrets.Forget(launch);
        if (_plans.TryRemove(launch, out var plan))
        {
            sandbox.Discard(plan);
        }

        SessionHome.Remove(SessionHomePaths.For(options.SindriHome, launch));   // a token renewal racing this writes nothing: TokenFile.Replace(create: false) never makes a folder

        await stages.Release(launch, cancellationToken).ConfigureAwait(false);
    }

    // why: a skill launch has no longMax (null), so its state timeout is the bound and the fallback must not cap it back to 2 h (RCP-SKILL-001).
    private static TimeSpan LongMaxOf(SandboxRequest request) =>
        request.LongMax ?? (request.SkillEnvironment is not null ? request.StepTimeout ?? _fallbackLongMax : _fallbackLongMax);
}
