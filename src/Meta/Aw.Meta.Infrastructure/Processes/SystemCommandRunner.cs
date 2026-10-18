using System.ComponentModel;
using System.Diagnostics;
using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Commands;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Processes;

/// <summary>
/// ICommandRunner over System.Diagnostics.Process. Both streams are read from the start, kept to their tail, and
/// appended to the request's log file as they arrive. On a timeout or a cancellation the whole process tree is killed.
/// </summary>
internal sealed class SystemCommandRunner : ICommandRunner
{
    private readonly TimeSpan _drainGrace;
    private readonly TimeSpan _termGrace;

    public SystemCommandRunner()
        : this(TimeSpan.FromSeconds(2))
    {
    }

    /// <summary>A runner that waits this long, after the command ends, for its output to finish.</summary>
    /// <param name="drainGrace">The wait.</param>
    /// <param name="termGrace">How long a command gets to exit after SIGTERM before it is killed (default 45 seconds, above the longest step cleanup; a request can set its own).</param>
    internal SystemCommandRunner(TimeSpan drainGrace, TimeSpan? termGrace = null)
    {
        _drainGrace = drainGrace;
        _termGrace = termGrace ?? TimeSpan.FromSeconds(45);
    }

    public async ValueTask<Result<CommandResult>> Run(CommandRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Argv.Count == 0)
        {
            return MetaErrors.CommandNotStarted.With("empty command");
        }

        using var sandbox = request.Isolated ? Sandbox.Create() : null;
        using var process = new Process { StartInfo = StartInfo(request, sandbox) };

        // The log is opened first: a log that can't be opened must not leave a started child with nobody to wait for it.
        StreamWriter? log;
        try
        {
#pragma warning disable CA2000 // disposed in the finally below
            log = OpenLog(request.LogFile);
#pragma warning restore CA2000
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return MetaErrors.CommandNotStarted.With($"log {request.LogFile}: {exception.Message}");
        }

        try
        {
            if (Start(process, request) is { } notStarted)
            {
                return notStarted;
            }

            return await Collect(process, request, log, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (log is not null)
            {
                await log.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private static Error? Start(Process process, CommandRequest request)
    {
        try
        {
            return process.Start() ? null : MetaErrors.CommandNotStarted.With(request.Argv[0]);
        }
        catch (Win32Exception exception)
        {
            return MetaErrors.CommandNotStarted.With($"{request.Argv[0]}: {exception.Message}");
        }
    }

    private async Task<Result<CommandResult>> Collect(Process process, CommandRequest request, StreamWriter? log, CancellationToken cancellationToken)
    {
        using var stopPumping = new CancellationTokenSource();
        var reading = Task.CompletedTask;
        try
        {
            var stdout = new TailBuffer(request.TailCharacters);
            var stderr = new TailBuffer(request.TailCharacters);
            process.StandardInput.Close();
#pragma warning disable CA2025 // awaited in the finally below, before the caller disposes the process and stopPumping goes out of scope
            reading = Task.WhenAll(
                Pump(process.StandardOutput, stdout, log, stopPumping.Token), Pump(process.StandardError, stderr, log, stopPumping.Token));
#pragma warning restore CA2025
            var timedOut = await Wait(process, request, request.Timeout, cancellationToken).ConfigureAwait(false);
            _ = await Task.WhenAny(reading, Task.Delay(_drainGrace, CancellationToken.None)).ConfigureAwait(false);
            return new CommandResult(timedOut ? -1 : process.ExitCode, stdout.ToString(), stderr.ToString(), timedOut);
        }
        finally
        {
            // A grandchild can keep the pipes open after the command ended: after the grace the pumps are cancelled and
            // awaited, so none still reads the process's streams or writes the log once the caller disposes them.
            await stopPumping.CancelAsync().ConfigureAwait(false);
            try
            {
                await reading.ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is OperationCanceledException or IOException or ObjectDisposedException)
            {
                // The output a grandchild produced after the grace is dropped, and a pump that failed (a log that can't
                // be written, a pipe that closed) loses output only: the command's own result stands.
            }
        }
    }

    private static ProcessStartInfo StartInfo(CommandRequest request, Sandbox? sandbox)
    {
        var (argv, unset, environment) = Plan(request, sandbox);
        var info = new ProcessStartInfo(ProgramPath.Resolve(argv[0], request.Cwd))
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in argv.Skip(1))
        {
            info.ArgumentList.Add(argument);
        }

        if (request.Cwd is { Length: > 0 } cwd)
        {
            info.WorkingDirectory = cwd;
        }

        foreach (var name in unset)
        {
            _ = info.Environment.Remove(name);
        }

        foreach (var (name, value) in environment)
        {
            info.Environment[name] = value;
        }

        return info;
    }

    // What to run and with which environment. An isolated command gets the sandbox in our process environment, or, behind a wrapper, through "env".
    private static (IReadOnlyList<string> Argv, IEnumerable<string> Unset, IEnumerable<KeyValuePair<string, string>> Env) Plan(CommandRequest request, Sandbox? sandbox)
    {
        if (sandbox is null)
        {
            return (request.Argv, request.UnsetEnv, request.Env);
        }

        return request.IsolatedFrom > 0 && request.IsolatedFrom < request.Argv.Count
            ? (WithEnv(request.Argv, request.IsolatedFrom, sandbox), request.UnsetEnv, request.Env)
            : (request.Argv, request.UnsetEnv.Concat(Sandbox.Unset), sandbox.Variables().Concat(request.Env));
    }

    // [wrapper..., env -u X... NAME=value..., command...]: the sandbox reaches the command and not the wrapper.
    private static List<string> WithEnv(IReadOnlyList<string> argv, int from, Sandbox sandbox)
    {
        var words = new List<string>(argv.Take(from)) { "env" };
        foreach (var name in Sandbox.Unset)
        {
            words.Add("-u");
            words.Add(name);
        }

        words.AddRange(sandbox.Variables().Select(pair => pair.Key + "=" + pair.Value));
        words.AddRange(argv.Skip(from));
        return words;
    }

    private static StreamWriter? OpenLog(string? file)
    {
        if (file is null)
        {
            return null;
        }

        if (Path.GetDirectoryName(Path.GetFullPath(file)) is { } directory)
        {
            _ = Directory.CreateDirectory(directory);
        }

        return new StreamWriter(new FileStream(file, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = true };
    }

    // Returns true when the command was killed for running past its timeout; a cancellation kills it and propagates.
    private async ValueTask<bool> Wait(Process process, CommandRequest request, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
            return false;
        }
        catch (OperationCanceledException)
        {
            await Stop(process, request.TermGrace ?? _termGrace).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return true;
        }
    }

    // SIGTERM to the command and every descendant it has now (pids this runner started, found through their parents, never by name), so
    // the heavy-job wrapper and the scripts run their exit traps and release their locks; then, after the grace, every snapshot process
    // still alive with the recorded start time is killed, and so is whatever it started since. The root exiting early does not end the
    // wait: its children are reparented, and are only found again through the snapshot.
    private static async Task Stop(Process process, TimeSpan grace)
    {
        var snapshot = ProcessTree.Snapshot(process.Id);
        if (snapshot.Count > 0 && await ProcessTree.Terminate([.. snapshot.Select(p => p.Pid)]).ConfigureAwait(false))
        {
            await WaitUntilGone(process, snapshot, grace).ConfigureAwait(false);
        }

        var alive = snapshot.Where(ProcessTree.IsAlive).ToList();
        var doomed = new HashSet<int>(alive.Select(p => p.Pid));
        foreach (var survivor in alive)
        {
            doomed.UnionWith(ProcessTree.Of(survivor.Pid));
        }

        if (doomed.Count > 0)
        {
            _ = await ProcessTree.Kill([.. doomed]).ConfigureAwait(false);
        }

        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Already gone.
        }

        await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
    }

    private static async Task WaitUntilGone(Process process, IReadOnlyList<ProcessRef> snapshot, TimeSpan grace)
    {
        using var expired = new CancellationTokenSource(grace);
        try
        {
            await process.WaitForExitAsync(expired.Token).ConfigureAwait(false);
            while (snapshot.Any(ProcessTree.IsAlive))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50), expired.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Still running after the grace: killed by the caller.
        }
    }

    private static async Task Pump(StreamReader reader, TailBuffer tail, StreamWriter? log, CancellationToken stop)
    {
        var buffer = new char[4096];
        int read;
        while ((read = await reader.ReadAsync(buffer.AsMemory(), stop).ConfigureAwait(false)) > 0)
        {
            lock (tail)
            {
                tail.Append(buffer.AsSpan(0, read));
            }

            if (log is not null)
            {
                lock (log)
                {
                    log.Write(buffer, 0, read);
                }
            }
        }
    }
}
