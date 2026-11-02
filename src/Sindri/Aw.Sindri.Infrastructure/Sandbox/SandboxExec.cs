using System.Diagnostics;
using Aw.Kernel;
using Aw.Kernel.Data.Processes;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>
/// 'sindri sandbox exec' (sandbox spec §3.6): runs a command in a fresh sandbox built from an ended launch's stored plan (<see cref="SandboxPlan.ForExec"/>: read-only worktree, ref directories and run
/// directory, fresh scratch, no launch secret, no GitHub token), under its own throw-away egress, inside the Steps' slice and counted against the session cap.
/// </summary>
/// <param name="sandbox">The sandbox that wraps the command.</param>
/// <param name="cap">The concurrent-session cap.</param>
/// <param name="egress">Starts the exec's own listeners.</param>
/// <param name="profile">The approved profile: the cap and the limits.</param>
/// <param name="box">The box's RAM and cores.</param>
/// <param name="systemdRun">The systemd-run program.</param>
/// <param name="launchDirectory">The launcher's directory, read when a launch's plan is looked up.</param>
internal sealed class SandboxExec(ISandbox sandbox, ISessionCap cap, EgressService egress, IApprovedProfile profile, BoxFacts box, string systemdRun, Func<string> launchDirectory) : ISandboxExec
{
    private static readonly TimeSpan _unusedLongMax = TimeSpan.FromHours(2);   // the exec is attended: it has no wall, so no longMax applies

    /// <inheritdoc/>
    public string PlanFileOf(LaunchId launch) => LaunchPaths.PlanFile(launchDirectory(), launch);

    /// <inheritdoc/>
    public async Task<Result<int>> Run(SandboxPlan stored, IReadOnlyList<string> command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stored);
        ArgumentNullException.ThrowIfNull(command);
        var probed = sandbox.Probe();
        if (probed.IsError)
        {
            return probed.Error;
        }

        var launch = LaunchId.For(Guid.CreateVersion7());
        var planned = Plan(stored, launch);
        if (planned.IsError)
        {
            return planned.Error;
        }

        var plan = planned.Value;
        var approved = await profile.Latest(cancellationToken).ConfigureAwait(false);
        var settings = approved.IsSuccess ? approved.Value?.Profile.Sandbox : null;
        return await Execute(plan, command, settings, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<int>> Execute(SandboxPlan plan, IReadOnlyList<string> command, SandboxSettings? settings, CancellationToken cancellationToken)
    {
        var limits = ScopeLimits.From(settings?.Limits, box, stepTimeout: null, _unusedLongMax, attended: true);
        RunningEgress? running = null;
        try
        {
            var started = await cap.Start(settings?.Limits?.Sessions ?? SandboxDefaults.Sessions, ct => Start(plan, command, limits, ct), cancellationToken).ConfigureAwait(false);
            if (started.IsError)
            {
                return started.Error;
            }

            running = started.Value.Egress;
            // why: disposing the pinned process releases its forking thread, and bwrap --die-with-parent dies with that thread, so it is disposed only after the wait
            using var pinned = started.Value.Pinned;
            await pinned.Process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return pinned.Process.ExitCode;
        }
        catch (OperationCanceledException)
        {
            return SindriErrors.NotRunning.With("the exec was cancelled");
        }
        finally
        {
            if (running is not null)
            {
                await running.DisposeAsync().ConfigureAwait(false);
            }

            Cleanup(plan);
        }
    }

    private static Result<SandboxPlan> Plan(SandboxPlan stored, LaunchId launch)
    {
        var area = RunAreaPreparer.RunAreaOf(stored);
        if (area is null)
        {
            return stored.ForExec(launch, "unused", "unused");   // names the missing run area
        }

        var id = launch.Value["LCH-".Length..];
        // why: the socket directory sits beside the stored one (/run/user/<uid>/aw-net or /tmp/aw-net), short because a unix socket path holds about 100 characters
        var netDir = Path.Combine(Path.GetDirectoryName(stored.Network.NetDir) ?? Path.GetTempPath(), id[..12]);
        return stored.ForExec(launch, Path.Combine(Path.GetDirectoryName(area) ?? area, "exec-" + id[..12]), netDir);
    }

    /// <summary>Under the cap's lock: the egress, the wrap and the process, so the scope exists when the lock is released.</summary>
    private async Task<Result<Started>> Start(SandboxPlan plan, IReadOnlyList<string> command, ScopeLimits limits, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var listeners = egress.Start(plan.Network, EgressCaps.Default);
        if (listeners.IsError)
        {
            return listeners.Error;
        }

        var wrapped = sandbox.Wrap(plan, command);
        if (wrapped.IsError)
        {
            _ = listeners.Value.DisposeAsync().AsTask();
            return wrapped.Error;
        }

        var argv = SystemdScopeArgv.Start(systemdRun, plan.Launch, limits, wrapped.Value);
        var info = ChildEnvironment.Start(argv[0], argv.Skip(1));   // no redirection: the command has this terminal

        try
        {
            var pinned = await PinnedProcess.StartAsync(info).ConfigureAwait(false);
            return new Started(pinned, listeners.Value);
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _ = listeners.Value.DisposeAsync().AsTask();
            return SandboxRefusal.For(SindriErrors.LaunchSelfCheckFailed, SandboxReasons.NoUserManager, $"{argv[0]} could not start: {e.Message}");
        }
    }

    // The exec's own run area (scratch, spool, plan) and secrets directory go with it; the stored launch's files are never touched.
    private void Cleanup(SandboxPlan plan)
    {
        sandbox.Discard(plan);
        var shortTemp = SandboxPaths.SeatbeltTemp(plan.Launch);   // Seatbelt's per-launch temp directory sits outside the run area
        if (plan.Mounts.Any(m => string.Equals(m.Path, shortTemp, StringComparison.Ordinal)) && Directory.Exists(shortTemp))
        {
            try
            {
                Directory.Delete(shortTemp, recursive: true);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Trace.TraceWarning($"could not remove the exec temp directory {shortTemp}: {e.Message}");
            }
        }

        if (RunAreaPreparer.RunAreaOf(plan) is { } area && Directory.Exists(area))
        {
            try
            {
                Directory.Delete(area, recursive: true);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Trace.TraceWarning($"could not remove the exec area {area}: {e.Message}");
            }
        }
    }

    private sealed record Started(PinnedProcess Pinned, RunningEgress Egress);
}
