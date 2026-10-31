using System.Globalization;
using System.Text;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>
/// The macOS sandbox (sandbox spec §3.1, SB-9c): the Seatbelt plan from the pure builder, rendered as an SBPL profile and run under 'sandbox-exec -f', proven by the canaries.
/// The environment is cleared with <c lang="text">env -i</c> outside the sandbox, the launcher-supplied variables come from a file the wrapper sources and deletes, and the proxy's
/// loopback port (known only once the egress runs) is put into the plan just before the profile is rendered.
/// </summary>
/// <param name="options">The programs and the self-check time.</param>
/// <param name="processes">Runs the helper programs.</param>
/// <param name="secrets">The per-launch values of the variables the plan leaves to the launcher.</param>
/// <param name="clock">The clock the self-check deadline is read against.</param>
/// <param name="proxyPort">The running egress's loopback port for a launch, or null when it has none.</param>
/// <param name="shortTemp">The launch's short temp directory (production: <see cref="SandboxPaths.SeatbeltTemp"/>; a test points it at a folder it owns).</param>
/// <param name="warn">Where a failure that must not stop the launcher (Discard) is reported; null drops it.</param>
internal sealed class SeatbeltSandbox(SeatbeltOptions options, ISandboxProcesses processes, ILaunchSecrets secrets, TimeProvider clock, Func<LaunchId, int?> proxyPort, Func<LaunchId, string> shortTemp, Action<string>? warn = null) : ISandbox
{
    // $1 is the secrets file (sourced, then deleted), the rest is the command.
    private const string SecretsWrapper = ". \"$1\" && rm -f \"$1\" && shift && exec \"$@\"";
    private const string ChangeDirectory = "cd \"$0\" && exec \"$@\"";
    private const string Env = "/usr/bin/env";

    private static readonly TimeSpan _probeTimeout = TimeSpan.FromSeconds(10);

    /// <inheritdoc />
    public Result<SandboxCapabilities> Probe()
    {
        var run = processes.Run(options.SandboxExec, ["-p", "(version 1)(allow default)", "/usr/bin/true"], _probeTimeout);
        return run.Succeeded
            ? new SandboxCapabilities(SandboxMechanism.Seatbelt, "sandbox-exec")
            : SandboxRefusal.For(SindriErrors.LaunchSandboxUnavailable, SandboxReasons.NoSeatbelt, FirstLine(run.Error, "sandbox-exec does not run here"));
    }

    /// <inheritdoc />
    public Result<SandboxPlan> Plan(SandboxRequest request) => SandboxPlanner.Build(SandboxMechanism.Seatbelt, request);   // the planner also runs SeatbeltPlan.Check

    /// <inheritdoc />
    public Result<IReadOnlyList<string>> Wrap(SandboxPlan plan, IReadOnlyList<string> argv)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(argv);
        if (PrepareOnce(plan) is { } refused)
        {
            return refused;
        }

        return WrapWith(plan, argv, secrets.For(plan.Launch));
    }

    /// <inheritdoc />
    public void Discard(SandboxPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        Remove(SecretsDirectory(plan), "secrets directory");   // this launch's directory only
        Remove(shortTemp(plan.Launch), "temp directory");
    }

    /// <inheritdoc />
    public async Task<Result<Unit>> SelfCheck(SandboxPlan plan, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (PrepareOnce(plan) is { } refused)
        {
            return refused;
        }

        var start = clock.GetTimestamp();
        try
        {
            foreach (var canary in SandboxCanaries.For(plan, seatbelt: true))
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

    private void Remove(string? directory, string what)
    {
        if (directory is null || !Directory.Exists(directory))
        {
            return;
        }

        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            warn?.Invoke($"could not remove the launch's {what} {directory}: {e.Message}");   // Discard runs on the failure path and must not replace the launch's own error
        }
    }

    // The host side of the plan is made before a wrap: the run area (-015 / -016 on a missing worktree or run directory), then the launch's short temp, made 0700 and emptied (a launch id relaunched after a crash reuses it).
    private Error? PrepareOnce(SandboxPlan plan)
    {
        try
        {
            if (RunAreaPreparer.Prepare(plan) is { } refusal)
            {
                return refusal;
            }

            ResetDirectory(shortTemp(plan.Launch));
            return null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return SandboxRefusal.For(SindriErrors.LaunchSelfCheckFailed, SandboxReasons.FsWrite, "the run area could not be prepared: " + e.Message);
        }
    }

    private static void ResetDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }

        _ = Directory.CreateDirectory(directory);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    private Result<IReadOnlyList<string>> WrapWith(SandboxPlan plan, IReadOnlyList<string> argv, IReadOnlyDictionary<string, string> values)
    {
        if (RunAreaPreparer.RunAreaOf(plan) is not { } area)
        {
            return SandboxRefusal.For(SindriErrors.LaunchSelfCheckFailed, SandboxReasons.FsWrite, "the plan names no run area");
        }

        if (proxyPort(plan.Launch) is not (> 0 and var port))
        {
            return SandboxRefusal.For(SindriErrors.LaunchSelfCheckFailed, SandboxReasons.NetForward, "the egress proxy is not running for this launch");
        }

        var names = BubblewrapArgv.LauncherSupplied(plan).Where(values.ContainsKey).ToList();
        if (names.Find(static n => !IsName(n)) is { } bad)
        {
            return SandboxRefusal.For(SindriErrors.LaunchSelfCheckFailed, SandboxReasons.Env, $"'{bad}' is not a variable name");
        }

        try
        {
            var withPort = WithProxy(plan, port);
            var profile = Path.Combine(area, "seatbelt.sb");
            OwnerOnly.WriteAtomically(profile, Encoding.UTF8.GetBytes(SeatbeltProfile.Render(withPort, area)));
            List<string> prefix = [Env, "-i", .. withPort.Environment.Where(e => e.Value is not null).Select(e => $"{e.Name}={e.Value}")];
            List<string> inner = [options.SandboxExec, "-f", profile, "/bin/sh", "-c", ChangeDirectory, plan.Worktree ?? plan.RunDirectory, .. argv];
            if (names.Count == 0)
            {
                return Results.Success<IReadOnlyList<string>>([.. prefix, .. inner]);
            }

            var file = WriteSecrets(plan, names.Select(n => (n, values[n])));
            return Results.Success<IReadOnlyList<string>>([.. prefix, "/bin/sh", "-c", SecretsWrapper, "sh", file, .. inner]);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return SandboxRefusal.For(SindriErrors.LaunchSelfCheckFailed, SandboxReasons.FsWrite, "the profile could not be written: " + e.Message);
        }
    }

    // The plan was built before the proxy had a port; the profile and the proxy variables take the real one.
    private static SandboxPlan WithProxy(SandboxPlan plan, int port)
    {
        var before = string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{plan.Network.ProxyPort}");
        var after = string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}");
        return plan with
        {
            Network = plan.Network with { ProxyPort = port },
            Environment = [.. plan.Environment.Select(e => e.Value is { } v && string.Equals(v, before, StringComparison.Ordinal) ? e with { Value = after } : e)],
        };
    }

    private static string? SecretsDirectory(SandboxPlan plan) =>
        RunAreaPreparer.RunAreaOf(plan) is { } area ? $"{area}/secrets/{plan.Launch.Value}" : null;

    // 'export NAME=value' lines, single-quoted: the wrapper sources the file, then deletes it.
    private static string WriteSecrets(SandboxPlan plan, IEnumerable<(string Name, string Value)> pairs)
    {
        var directory = SecretsDirectory(plan) ?? throw new IOException("the plan names no run area");
        OwnerOnly.EnsureDirectory(directory);
        foreach (var old in Directory.GetFiles(directory))
        {
            File.Delete(old);   // a retried Wrap, or a file a crash left: only the newest wrapper's file may exist
        }

        var file = Path.Combine(directory, Path.GetRandomFileName());
        var text = string.Concat(pairs.Select(p => $"export {p.Name}={SandboxCanaries.Quote(p.Value)}\n"));
        OwnerOnly.WriteAtomically(file, Encoding.UTF8.GetBytes(text));
        return file;
    }

    private string? Run(SandboxPlan plan, SandboxCanary canary, TimeSpan timeout)
    {
        var wrapped = WrapWith(plan, [canary.Shell, "-c", canary.Script], new Dictionary<string, string>(StringComparer.Ordinal));   // a canary needs no secret
        if (wrapped.IsError)
        {
            return wrapped.Error.Message;
        }

        var result = processes.Run(wrapped.Value[0], [.. wrapped.Value.Skip(1)], timeout);
        if (!result.Started)
        {
            return "sandbox-exec could not be started";
        }

        if (result.TimedOut)
        {
            return $"timed out after {timeout.TotalSeconds.ToString("0.#", CultureInfo.InvariantCulture)} s";
        }

        if (result.ExitCode != 0)
        {
            var line = FirstLine((result.Output + result.Error).Trim(), $"exit {result.ExitCode.ToString(CultureInfo.InvariantCulture)}");
            return line.Length > 300 ? line[..300] : line;
        }

        return canary.Judge?.Invoke(result);
    }

    // The git-ref canary never deletes a ref from inside; the refs it made are removed from outside, as the owner.
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

    private static string FirstLine(string text, string fallback) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? fallback;

    private static bool IsName(string name) =>
        name.Length > 0 && (char.IsAsciiLetter(name[0]) || name[0] == '_') && name.All(static c => char.IsAsciiLetterOrDigit(c) || c == '_');
}
