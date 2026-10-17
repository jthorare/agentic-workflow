using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Aw.Sindri.Infrastructure.Processes;

/// <summary>IToolRunner over System.Diagnostics.Process: no shell, standard output captured, standard error read line by line (handed to the caller's callback, and kept, up to a cap, for a caller that classifies a failure by it).</summary>
internal sealed class ProcessToolRunner : IToolRunner
{
    private const int ErrorCap = 16 * 1024;

    /// <inheritdoc />
    public ToolRun Run(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout, ToolRunOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ToolRun.Cancelled;
        }

        var start = new ProcessStartInfo(fileName, arguments)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        ApplyEnvironment(start, options?.Environment);
        Process? process;
        try
        {
            process = Process.Start(start);
        }
        catch (Win32Exception)
        {
            return ToolRun.Missing;   // not on PATH
        }

        if (process is null)
        {
            return ToolRun.Missing;
        }

        using (process)
        {
            return Wait(process, timeout, options?.StandardErrorLine, cancellationToken);
        }
    }

    private static void ApplyEnvironment(ProcessStartInfo start, IReadOnlyDictionary<string, string?>? changes)
    {
        if (changes is null)
        {
            return;
        }

        foreach (var (name, value) in changes)
        {
            if (value is null)
            {
                _ = start.Environment.Remove(name);
            }
            else
            {
                start.Environment[name] = value;
            }
        }
    }

    private static ToolRun Wait(Process process, TimeSpan timeout, Action<string>? standardErrorLine, CancellationToken cancellationToken)
    {
        process.StandardInput.Close();   // a tool that prompts reads end of input instead of waiting for a terminal
        var output = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var error = DrainLines(process.StandardError, standardErrorLine);
        var killedByCancel = false;
        using var registration = cancellationToken.Register(() =>
        {
            if (process.HasExited)
            {
                return;   // it finished on its own before the cancel: its result stands
            }

            Volatile.Write(ref killedByCancel, value: true);
            Kill(process);
        });
        if (!process.WaitForExit(timeout))
        {
            Kill(process);
            process.WaitForExit();
            return ToolRun.TimedOut;
        }

        process.WaitForExit();   // flushes the redirected streams
        var errorText = error.GetAwaiter().GetResult();
        var text = output.GetAwaiter().GetResult();
        return Volatile.Read(ref killedByCancel) ? ToolRun.Cancelled : new ToolRun(ToolOutcome.Exited, process.ExitCode, text, errorText);
    }

    private static async Task<string> DrainLines(StreamReader reader, Action<string>? onLine)
    {
        var kept = new StringBuilder();
        while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
        {
            onLine?.Invoke(line);
            if (kept.Length < ErrorCap)
            {
                _ = kept.Append(line).Append('\n');   // the rest is still drained so the tool never blocks on a full pipe
            }
        }

        return kept.ToString();
    }

    private static void Kill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // it already exited
        }
    }
}
