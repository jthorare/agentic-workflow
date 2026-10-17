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

    public SystemCommandRunner()
        : this(TimeSpan.FromSeconds(2))
    {
    }

    /// <summary>A runner that waits this long, after the command ends, for its output to finish.</summary>
    /// <param name="drainGrace">The wait.</param>
    internal SystemCommandRunner(TimeSpan drainGrace)
    {
        _drainGrace = drainGrace;
    }

    public async ValueTask<Result<CommandResult>> Run(CommandRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Argv.Count == 0)
        {
            return MetaErrors.CommandNotStarted.With("empty command");
        }

        using var process = new Process { StartInfo = StartInfo(request) };
        try
        {
            if (!process.Start())
            {
                return MetaErrors.CommandNotStarted.With(request.Argv[0]);
            }
        }
        catch (Win32Exception exception)
        {
            return MetaErrors.CommandNotStarted.With($"{request.Argv[0]}: {exception.Message}");
        }

#pragma warning disable CA2000 // disposed in the finally below
        var log = OpenLog(request.LogFile);
#pragma warning restore CA2000
        try
        {
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
            var timedOut = await Wait(process, request.Timeout, cancellationToken).ConfigureAwait(false);
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

    private static ProcessStartInfo StartInfo(CommandRequest request)
    {
        var info = new ProcessStartInfo(ProgramPath.Resolve(request.Argv[0], request.Cwd))
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in request.Argv.Skip(1))
        {
            info.ArgumentList.Add(argument);
        }

        if (request.Cwd is { Length: > 0 } cwd)
        {
            info.WorkingDirectory = cwd;
        }

        foreach (var (name, value) in request.Env)
        {
            info.Environment[name] = value;
        }

        return info;
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
    private static async ValueTask<bool> Wait(Process process, TimeSpan timeout, CancellationToken cancellationToken)
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
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return true;
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
