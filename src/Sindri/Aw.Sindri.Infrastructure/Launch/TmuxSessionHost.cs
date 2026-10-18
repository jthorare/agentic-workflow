using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>
/// The tmux <see cref="ISessionHost"/> (API P10a, unit U5): sessions on the Sindri-owned server ('tmux -L sindri-launch'), named
/// 'snd-&lt;launchId&gt;'. Every call is one bounded tmux process with TMUX removed from its environment, so a Sindri that was
/// itself started from a tmux pane never reaches that pane's server. The session's environment goes in through
/// 'new-session -e' (tmux 3.2); the server's global environment is never written ('set-environment -g' is never run), so
/// one launch's secret can't leak into the next.
/// </summary>
internal sealed partial class TmuxSessionHost(TmuxHostOptions options) : ISessionHost
{
    private static readonly TimeSpan _callBudget = TimeSpan.FromSeconds(5);
    private static readonly (int Major, int Minor) _minimum = (3, 2);   // 'new-session -e' arrived in 3.2

    /// <inheritdoc/>
    public HostKind Kind => HostKind.Tmux;

    /// <inheritdoc/>
    public async ValueTask<Result<HostRef>> Start(HostSession session, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.Argv.Count == 0)
        {
            return KernelErrors.Invalid.With("A session needs a command to run");
        }

        var name = new HostRef(HostKind.Tmux, session.Name);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);   // one deadline over the whole start: the version check and new-session share it
        try
        {
            return await HostProblem(deadline.Token).ConfigureAwait(false) is { } problem
                ? problem
                : await Launch(session, name, deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _ = await Stop(name, CancellationToken.None).ConfigureAwait(false);   // overran: the session must not appear later
            return SindriErrors.LaunchTimedOut.With($"tmux did not start the session within {timeout.TotalSeconds.ToString("0.#", CultureInfo.InvariantCulture)} s");
        }
        catch (OperationCanceledException)
        {
            _ = await Stop(name, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        catch (Win32Exception)
        {
            return SindriErrors.LaunchTimedOut.With("tmux is not installed");
        }
    }

    private async ValueTask<Result<HostRef>> Launch(HostSession session, HostRef name, CancellationToken deadline)
    {
        var created = await Run(Create(session), deadline).ConfigureAwait(false);
        if (created.ExitCode == 0)
        {
            return name;
        }

        return created.Error.Contains("duplicate session", StringComparison.Ordinal)
            ? KernelErrors.Conflict.With($"A session named {session.Name} already exists")
            : SindriErrors.LaunchTimedOut.With($"tmux could not start the session: {Short(created.Error)}");
    }

    /// <inheritdoc/>
    public async ValueTask<Result<Unit>> Stop(HostRef host, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(host);
        try
        {
            var killed = await Run(["kill-session", "-t", Target(host)], cancellationToken, _callBudget).ConfigureAwait(false);
            return killed.ExitCode == 0 || IsGone(killed.Error)
                ? Unit.Value
                : KernelErrors.Unavailable.With($"tmux could not end {host.Name}: {Short(killed.Error)}");
        }
        catch (Win32Exception)
        {
            return Unit.Value;   // no tmux, so no session to end
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return KernelErrors.Unavailable.With($"tmux did not answer while ending {host.Name}");
        }
    }

    /// <inheritdoc/>
    public async ValueTask<int> AttachedClients(HostRef host, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(host);
        try
        {
            var listed = await Run(["list-clients", "-t", Target(host), "-F", "#{client_name}"], cancellationToken, _callBudget).ConfigureAwait(false);
            return listed.ExitCode == 0 ? listed.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length : 0;
        }
        catch (Win32Exception)
        {
            return 0;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return 0;
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> AttachArgv(HostRef host)
    {
        ArgumentNullException.ThrowIfNull(host);
        return [options.Executable, "-L", options.Socket, "attach", "-t", Target(host)];
    }

    /// <inheritdoc/>
    public async ValueTask<HostFacts> Probe(CancellationToken cancellationToken)
    {
        try
        {
            var banner = await Exec(["-V"], socket: false, _callBudget, cancellationToken).ConfigureAwait(false);
            var match = VersionPattern.Match(banner.Output);
            if (banner.ExitCode != 0 || !match.Success)
            {
                return banner.ExitCode == 0 && banner.Output.Contains("tmux", StringComparison.Ordinal)
                    ? new HostFacts(HostKind.Tmux, Present: true, banner.Output.Replace("tmux", "", StringComparison.Ordinal).Trim(), Problem: null)   // a development build ('master')
                    : new HostFacts(HostKind.Tmux, Present: false, Version: null, "not-installed");
            }

            var (major, minor) = (int.Parse(match.Groups["major"].Value, CultureInfo.InvariantCulture), int.Parse(match.Groups["minor"].Value, CultureInfo.InvariantCulture));
            var version = string.Create(CultureInfo.InvariantCulture, $"{major}.{minor}");
            return (major, minor).CompareTo(_minimum) >= 0
                ? new HostFacts(HostKind.Tmux, Present: true, version, Problem: null)
                : new HostFacts(HostKind.Tmux, Present: false, version, "too-old");
        }
        catch (Win32Exception)
        {
            return new HostFacts(HostKind.Tmux, Present: false, Version: null, "not-installed");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new HostFacts(HostKind.Tmux, Present: false, Version: null, "not-installed");
        }
    }

    private async ValueTask<Error?> HostProblem(CancellationToken cancellationToken)
    {
        var facts = await Probe(cancellationToken).ConfigureAwait(false);
        return facts.Present
            ? null
            : SindriErrors.LaunchTimedOut.With($"The session host is not usable here: tmux {facts.Problem}" + (facts.Version is null ? "" : $" ({facts.Version}; 3.2 or newer is needed)"));
    }

    [GeneratedRegex(@"(?<major>\d+)\.(?<minor>\d+)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex VersionPattern { get; }

    private static string Target(HostRef host) => "=" + host.Name;   // '=' : this exact name, never a prefix

    private static bool IsGone(string error) =>
        error.Contains("can't find session", StringComparison.Ordinal)
        || error.Contains("no server running", StringComparison.Ordinal)
        || (error.Contains("error connecting to", StringComparison.Ordinal)
            && (error.Contains("(No such file or directory)", StringComparison.Ordinal) || error.Contains("(Connection refused)", StringComparison.Ordinal)));   // a permission error is not "gone"

    private static string Short(string text) => text.Trim() is { Length: > 200 } t ? t[..200] : text.Trim();

    private static List<string> Create(HostSession session)
    {
        var args = new List<string> { "new-session", "-d", "-s", session.Name, "-c", session.WorkDir };
        foreach (var (key, value) in session.Env)
        {
            args.Add("-e");
            args.Add($"{key}={value}");
        }

        args.AddRange(session.Argv);
        return args;
    }

    private Task<TmuxRun> Run(IReadOnlyList<string> args, CancellationToken cancellationToken, TimeSpan? budget = null) =>
        Exec(args, socket: true, budget, cancellationToken);

    private async Task<TmuxRun> Exec(IReadOnlyList<string> args, bool socket, TimeSpan? budget, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(options.Executable) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        if (socket)
        {
            start.ArgumentList.Add("-L");
            start.ArgumentList.Add(options.Socket);
        }

        foreach (var arg in args)
        {
            start.ArgumentList.Add(arg);
        }

        _ = start.Environment.Remove("TMUX");   // a pane's own server is never ours
        if (options.TmuxTmpDir is { } tmpDir)
        {
            start.Environment["TMUX_TMPDIR"] = tmpDir;
            _ = Directory.CreateDirectory(tmpDir);
        }

        using var process = Process.Start(start) ?? throw new Win32Exception("tmux did not start");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (budget is { } limit)
        {
            deadline.CancelAfter(limit);
        }

        try
        {
            var output = process.StandardOutput.ReadToEndAsync(deadline.Token);
            var error = process.StandardError.ReadToEndAsync(deadline.Token);
            await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
            return new TmuxRun(process.ExitCode, (await output.ConfigureAwait(false)).Trim(), (await error.ConfigureAwait(false)).Trim());
        }
        catch (OperationCanceledException)
        {
            await Reap(process).ConfigureAwait(false);   // never leave a tmux client running; the server it may have started is the caller's to clean up
            throw;
        }
    }

    private static async Task Reap(Process process)
    {
        try
        {
            process.Kill();
        }
        catch (InvalidOperationException)
        {
            return;   // it ended between the cancel and the kill
        }

        using var wait = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try
        {
            await process.WaitForExitAsync(wait.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // killed and still not reaped after 2 s: the OS will; the caller must not hang on it
        }
    }

    private readonly record struct TmuxRun(int ExitCode, string Output, string Error);
}
