using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Processes;

namespace Aw.Judge.Infrastructure.Processes;

/// <summary>
/// IProcessRunner over System.Diagnostics.Process. Both output streams are read concurrently from the start, each kept
/// up to the request's output limit, and the input is written on its own task, so a program that fills a pipe never
/// blocks. On a timeout or a cancellation the whole process tree is killed. Once the program exits, the streams get a
/// short grace to finish: a background child it left holding the pipes can't keep the run open (that child is not
/// stopped: it is no longer in the program's tree).
/// </summary>
/// <param name="environment">Reads PATH, to find a program given by bare name.</param>
internal sealed class SystemProcessRunner(Func<string, string?> environment) : IProcessRunner
{
    // ENOENT on Unix and ERROR_FILE_NOT_FOUND on Windows: the program vanished between the lookup and the start (or the
    // working directory did).
    private const int NoSuchFile = 2;

    // After a kill, how long to wait for the pipes to drain before giving up on the rest of the output.
    private static readonly TimeSpan _drainGrace = TimeSpan.FromSeconds(2);

    /// <inheritdoc />
    public async ValueTask<ProcessResult> Run(ProcessRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (Resolve(request.FileName) is not { } file)
        {
            return ProcessResult.NotStarted();
        }

        using var process = new Process { StartInfo = StartInfo(file, request) };
        if (Start(process, file) is { } refused)
        {
            return refused;
        }

        var stdout = BoundedText.Read(process.StandardOutput, request.OutputLimit);
        var stderr = BoundedText.Read(process.StandardError, request.OutputLimit);
        var stdin = WriteInput(process.StandardInput, request.StandardInput);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(request.Timeout);
        try
        {
            await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            KillTree(process);
            cancellationToken.ThrowIfCancellationRequested();
            await Drained(stdout.Reading, stderr.Reading, stdin).ConfigureAwait(false);
            return ProcessResult.TimedOutAfter(stdout.Text, stderr.Text);
        }

        // The pipes close as the program exits, unless a background child it left kept them: wait only the grace for that.
        await Drained(stdout.Reading, stderr.Reading, stdin).ConfigureAwait(false);
        return ProcessResult.Exited(process.ExitCode, stdout.Text, stderr.Text);
    }

    // Null once it runs; otherwise why it didn't. ENOENT is a missing program only when the file itself is gone (the
    // working directory can vanish between the check above and the start, and is reported the same way).
    private static ProcessResult? Start(Process process, string file)
    {
        // The program is there; a working directory that isn't is the caller's bad path, not a missing program. (Unix
        // reports a failed chdir in the child as ENOENT too, which would otherwise read as 'not installed'.)
        if (process.StartInfo.WorkingDirectory is { Length: > 0 } directory && !Directory.Exists(directory))
        {
            return ProcessResult.FailedToStart($"working directory not found: {directory}");
        }

        try
        {
            return process.Start() ? null : ProcessResult.NotStarted();
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == NoSuchFile && !File.Exists(file))
        {
            return ProcessResult.NotStarted();
        }
        catch (Win32Exception exception)
        {
            // It is installed, so this is a real failure (E2BIG, ENOEXEC, EACCES...), not a missing program.
            return ProcessResult.FailedToStart(exception.Message);
        }
    }

    private static ProcessStartInfo StartInfo(string file, ProcessRequest request)
    {
        var info = new ProcessStartInfo(file)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = request.WorkingDirectory ?? "",
        };
        foreach (var argument in request.Arguments)
        {
            info.ArgumentList.Add(argument);
        }

        if (request.InheritOnly is { } inherit)
        {
            var kept = inherit.Select(name => (name, value: info.Environment.TryGetValue(name, out var value) ? value : null)).Where(pair => pair.value is not null).ToList();
            info.Environment.Clear();
            foreach (var (name, value) in kept)
            {
                info.Environment[name] = value;
            }
        }

        foreach (var (name, value) in request.Environment)
        {
            info.Environment[name] = value;
        }

        foreach (var name in request.Unset)
        {
            _ = info.Environment.Remove(name);
        }

        return info;
    }

    // A bare name is looked up on PATH as the environment gives it; anything with a directory part is used as is.
    private string? Resolve(string fileName) =>
        fileName.Contains(Path.DirectorySeparatorChar, StringComparison.Ordinal) || fileName.Contains(Path.AltDirectorySeparatorChar, StringComparison.Ordinal)
            ? SearchPath.IsExecutable(fileName) ? fileName : null
            : SearchPath.Find(environment("PATH"), fileName);

    // Writes the input and closes the stream; a program that exits without reading it (a broken pipe) is not an error.
    private static async Task WriteInput(StreamWriter input, string? text)
    {
        try
        {
            if (text is not null)
            {
                await input.WriteAsync(text).ConfigureAwait(false);
                await input.FlushAsync().ConfigureAwait(false);
            }

            input.Close();
        }
        catch (IOException)
        {
            // The program closed its end first; what it read is all it wanted.
        }
        catch (ObjectDisposedException)
        {
            // The run ended (and the process was disposed) while the write was still going.
        }
    }

    private static void KillTree(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // It exited between the timeout and the kill.
        }
        catch (Win32Exception)
        {
            // A process in the tree couldn't be signalled (it is already gone).
        }
    }

    // Waits up to the grace for the streams to finish; what they hold by then is the output.
    private static async Task Drained(params Task[] streams)
    {
        try
        {
            await Task.WhenAll(streams).WaitAsync(_drainGrace).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // A process that outlived the program still holds a pipe; keep what was read.
        }
    }
}
