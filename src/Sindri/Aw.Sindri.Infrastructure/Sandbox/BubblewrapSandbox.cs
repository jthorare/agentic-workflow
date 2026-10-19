using System.Globalization;
using System.Text;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>
/// The Linux sandbox (sandbox spec §3.1, §4, SB12): a plan from the pure builder, turned into a bubblewrap command line, and proven inside the real thing by the canaries.
/// The cgroup scope (SB-3a) and the network forwarders (SB-4a) wrap and join it later; this adapter is the mount stack, the environment and the self-check.
/// </summary>
/// <param name="options">The programs and the time the self-check may take.</param>
/// <param name="processes">Runs the helper programs.</param>
/// <param name="host">What the host's paths are.</param>
/// <param name="secrets">The per-launch values of the variables the plan leaves to the launcher.</param>
/// <param name="clock">The clock the self-check deadline is read against.</param>
/// <param name="warn">Where a failure that must not stop the launcher (Discard) is reported; null drops it.</param>
internal sealed class BubblewrapSandbox(BubblewrapOptions options, ISandboxProcesses processes, IHostPaths host, ILaunchSecrets secrets, TimeProvider clock, Action<string>? warn = null) : ISandbox
{
    // The wrapper reads the launcher-supplied --setenv pairs from a file it deletes at once, so no secret is ever an argument: $1 is the file, $2 bubblewrap, the rest its arguments.
    private const string SecretsWrapper = "f=$1; b=$2; shift 2; exec 3<\"$f\" && rm -f \"$f\" && exec \"$b\" \"$@\"";

    private static readonly TimeSpan _probeTimeout = TimeSpan.FromSeconds(10);

    /// <inheritdoc />
    public Result<SandboxCapabilities> Probe()
    {
        var bwrap = processes.Run(options.Bwrap, ["--version"], _probeTimeout);
        if (!bwrap.Succeeded)
        {
            return Unavailable(SandboxReasons.NoBwrap, "bubblewrap is not installed");
        }

        var userNamespace = processes.Run(options.Bwrap, ["--ro-bind", "/", "/", "--unshare-user", "--unshare-net", "true"], _probeTimeout);
        if (!userNamespace.Succeeded)
        {
            return Unavailable(SandboxReasons.NoUserNamespace, FirstLine(userNamespace.Error, "bubblewrap cannot create a sandbox here"));
        }

        var scope = processes.Run(options.SystemdRun, ["--user", "--scope", "--quiet", "true"], _probeTimeout);
        if (!scope.Succeeded)
        {
            return Unavailable(SandboxReasons.NoUserManager, FirstLine(scope.Error, "systemd-run --user --scope does not work for this account"));
        }

        if (MissingController() is { } missing)
        {
            return Unavailable(SandboxReasons.NoCgroupControllers, $"the account's cgroup lacks the {missing} controller");
        }

        var limits = processes.Run(options.SystemdRun, ["--user", "--scope", "--quiet", "-p", "MemoryMax=64M", "-p", "TasksMax=64", "true"], _probeTimeout);
        if (!limits.Succeeded)
        {
            return Unavailable(SandboxReasons.NoCgroupControllers, FirstLine(limits.Error, "a scope with a memory and a process limit cannot be made"));
        }

        if (!processes.Run(options.Socat, ["-V"], _probeTimeout).Succeeded)
        {
            return Unavailable(SandboxReasons.NoForwarder, "socat is not installed");
        }

        return new SandboxCapabilities(SandboxMechanism.Bubblewrap, FirstLine(bwrap.Output, "bubblewrap"));
    }

    /// <inheritdoc />
    public Result<SandboxPlan> Plan(SandboxRequest request) => SandboxPlanner.Build(SandboxMechanism.Bubblewrap, request);

    /// <inheritdoc />
    public Result<IReadOnlyList<string>> Wrap(SandboxPlan plan, IReadOnlyList<string> argv)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return WrapWith(plan, argv, secrets.For(plan.Launch), prepare: true);
    }

    /// <inheritdoc />
    public void Discard(SandboxPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (SecretsDirectory(plan) is { } directory && Directory.Exists(directory))
        {
            try
            {
                Directory.Delete(directory, recursive: true);   // this launch's directory only: another launch of the run area keeps its file
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Discard runs on the launcher's failure path: it must not replace the launch's own error. The 0600 file stays for the run area's cleanup.
                warn?.Invoke($"could not remove the launch's secrets directory {directory}: {e.Message}");
            }
        }
    }

    /// <inheritdoc />
    public async Task<Result<Unit>> SelfCheck(SandboxPlan plan, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (PolicyHidesTheLogin(plan) is { } refusal)
        {
            return refusal;
        }

        if (PrepareOnce(plan) is { } refused)
        {
            return refused;
        }

        var start = clock.GetTimestamp();
        try
        {
            foreach (var canary in SandboxCanaries.For(plan))
            {
                ct.ThrowIfCancellationRequested();
                var left = options.SelfCheckDeadline - clock.GetElapsedTime(start);
                if (left <= TimeSpan.Zero)
                {
                    return SandboxRefusal.For(SindriErrors.LaunchSelfCheckFailed, canary.Reason, $"the self-check took longer than {options.SelfCheckDeadline.TotalSeconds.ToString("0", CultureInfo.InvariantCulture)} s");
                }

                var observed = await Task.Run(() => Run(plan, canary, left < options.CanaryTimeout ? left : options.CanaryTimeout), ct).ConfigureAwait(false);
                if (observed is not null)
                {
                    return SandboxRefusal.For(SindriErrors.LaunchSelfCheckFailed, canary.Reason, observed);
                }
            }
        }
        finally
        {
            RemoveSelfCheckRefs(plan);
        }

        return Unit.Value;
    }

    // The host side of the plan is made once, before the first canary, and its refusal (-015 missing worktree, -016 missing run directory) is the launch's, not a canary's.
    private static Error? PrepareOnce(SandboxPlan plan)
    {
        try
        {
            return RunAreaPreparer.Prepare(plan);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return SandboxRefusal.For(SindriErrors.LaunchSelfCheckFailed, SandboxReasons.FsWrite, "the run area could not be prepared: " + e.Message);
        }
    }

    // The login canary also proves, on the plan, that the inner policy refuses the agent's tools the config directory.
    private static Error? PolicyHidesTheLogin(SandboxPlan plan)
    {
        var configs = plan.Environment
            .Where(e => string.Equals(e.Name, "CLAUDE_CONFIG_DIR", StringComparison.Ordinal) || string.Equals(e.Name, "CODEX_HOME", StringComparison.Ordinal))
            .Select(e => e.Value)
            .OfType<string>();
        var open = configs.FirstOrDefault(dir => !plan.Policy.Rules.Any(r => r.Effect == InnerRuleEffect.Deny && string.Equals(r.Tool, "Read", StringComparison.Ordinal) && r.Pattern.StartsWith(dir, StringComparison.Ordinal)));
        return open is null ? null : SandboxRefusal.For(SindriErrors.LaunchSelfCheckFailed, SandboxReasons.Login, $"the inner policy does not deny reading {open}");
    }

    private static string Observed(ProcessResult result)
    {
        var text = (result.Output + result.Error).Trim();
        var line = FirstLine(text, $"exit {result.ExitCode.ToString(CultureInfo.InvariantCulture)}");
        return line.Length > 300 ? line[..300] : line;
    }

    private static string FirstLine(string text, string fallback) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? fallback;

    private static Error Unavailable(string reason, string detail) => SandboxRefusal.For(SindriErrors.LaunchSandboxUnavailable, reason, detail);

    // The launcher's values for the variables the plan leaves open go to bubblewrap on a descriptor, from a 0600 file that the wrapper deletes as it opens it. Without any value the command is plain.
    private Result<IReadOnlyList<string>> WrapWith(SandboxPlan plan, IReadOnlyList<string> argv, IReadOnlyDictionary<string, string> values, bool prepare)
    {
        ArgumentNullException.ThrowIfNull(argv);
        try
        {
            if (prepare && RunAreaPreparer.Prepare(plan) is { } refusal)
            {
                return refusal;
            }

            var names = BubblewrapArgv.LauncherSupplied(plan);
            var pairs = names.Where(values.ContainsKey).ToList();
            if (pairs.Count == 0)
            {
                return Results.Success<IReadOnlyList<string>>([options.Bwrap, .. BubblewrapArgv.Build(plan, host, secretsFd: false), "--", .. argv]);
            }

            var file = WriteSecrets(plan, pairs.Select(n => (n, values[n])));
            return Results.Success<IReadOnlyList<string>>(["/bin/sh", "-c", SecretsWrapper, "sh", file, options.Bwrap, .. BubblewrapArgv.Build(plan, host, secretsFd: true), "--", .. argv]);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return SandboxRefusal.For(SindriErrors.LaunchSelfCheckFailed, SandboxReasons.FsWrite, "the run area could not be prepared: " + e.Message);
        }
    }

    // <run area>/secrets/<launch id>/: a run area may serve several launches, and a sweep or a discard must reach only its own.
    private static string? SecretsDirectory(SandboxPlan plan) =>
        RunAreaPreparer.RunAreaOf(plan) is { } area ? $"{area}/secrets/{plan.Launch.Value}" : null;

    private static string WriteSecrets(SandboxPlan plan, IEnumerable<(string Name, string Value)> pairs)
    {
        var directory = SecretsDirectory(plan) ?? throw new IOException("the plan names no run area");
        OwnerOnly.EnsureDirectory(directory);
        foreach (var old in Directory.GetFiles(directory))
        {
            File.Delete(old);   // a retried Wrap, or a file a crash left: only the newest wrapper's file may exist
        }

        var file = Path.Combine(directory, Path.GetRandomFileName());
        var text = string.Concat(pairs.Select(p => $"--setenv\0{p.Name}\0{p.Value}\0"));
        OwnerOnly.WriteAtomically(file, Encoding.UTF8.GetBytes(text));
        return file;
    }

    private string? Run(SandboxPlan plan, SandboxCanary canary, TimeSpan timeout)
    {
        var wrapped = WrapWith(plan, [canary.Shell, "-c", canary.Script], new Dictionary<string, string>(StringComparer.Ordinal), prepare: false);   // a canary needs no secret, and SelfCheck prepared the run area once
        if (wrapped.IsError)
        {
            return wrapped.Error.Message;
        }

        var result = processes.Run(wrapped.Value[0], [.. wrapped.Value.Skip(1)], timeout);
        if (!result.Started)
        {
            return "bubblewrap could not be started";
        }

        if (result.TimedOut)
        {
            return $"timed out after {timeout.TotalSeconds.ToString("0.#", CultureInfo.InvariantCulture)} s";
        }

        if (result.ExitCode != 0)
        {
            return Observed(result);
        }

        return canary.Judge?.Invoke(result);
    }

    // The git-ref canary never deletes a ref from inside; the refs it made (and one it must not have made) are removed from outside, as the owner.
    private void RemoveSelfCheckRefs(SandboxPlan plan)
    {
        if (plan.Worktree is null || !plan.Writable.Contains(plan.Worktree, StringComparer.Ordinal))
        {
            return;
        }

        foreach (var reference in new[] { $"refs/heads/{plan.RunNamespace}/selfcheck", "refs/heads/other-selfcheck" })
        {
            _ = processes.Run(options.Git, ["-C", plan.Worktree, "-c", "core.hooksPath=/dev/null", "-c", "core.fsmonitor=", "update-ref", "-d", reference], _probeTimeout);
        }
    }

    private string? MissingController()
    {
        var uid = host.Uid();
        return uid is null ? null : CgroupControllers.FirstMissing(host.ReadText(CgroupControllers.FileFor(uid.Value)));
    }
}
