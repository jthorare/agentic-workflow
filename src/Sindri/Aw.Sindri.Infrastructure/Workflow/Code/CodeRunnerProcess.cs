using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Infrastructure.Workflow.Code;

/// <summary>Runs a compiled body in a 'sindri-code-runner' process: the input goes in on stdin, the result comes back on stdout, and the process is killed at the timeout.</summary>
/// <param name="runner">The runner's command: the binary, or 'dotnet' and its dll.</param>
/// <param name="confine">Wraps the command in the sandbox, or null to run it as it is (tests).</param>
internal sealed class CodeRunnerProcess(IReadOnlyList<string> runner, Func<IReadOnlyList<string>, Result<IReadOnlyList<string>>>? confine = null)
{
    /// <summary>Runs the assembly's method on the input.</summary>
    /// <param name="assemblyPath">The cached assembly.</param>
    /// <param name="inputJson">The input as JSON.</param>
    /// <param name="limits">The timeout and the memory limit.</param>
    /// <param name="cancellationToken">Cancels the run and kills the process.</param>
    /// <returns>The result as JSON, or SND-CODE-002, -003, -004 or -005.</returns>
    public async Task<Result<string>> Run(string assemblyPath, string inputJson, CodeLimits limits, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(limits);
        IReadOnlyList<string> argv = [.. runner, assemblyPath, "--memory-mb", limits.MemoryMb.ToString(CultureInfo.InvariantCulture)];
        if (confine is not null)
        {
            var wrapped = confine(argv);
            if (wrapped.IsError)
            {
                return SindriErrors.CodeRunnerUnavailable.With(wrapped.Error.Message);
            }

            argv = wrapped.Value;
        }

        var start = ChildEnvironment.Start(argv[0], argv.Skip(1));
        start.RedirectStandardInput = true;
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        Process? process;
        try
        {
            process = Process.Start(start);
        }
        catch (Win32Exception e)   // boundary: no runner binary is an expected failure
        {
            return SindriErrors.CodeRunnerUnavailable.With(e.Message);
        }

        using (process)
        {
            return process is null ? SindriErrors.CodeRunnerUnavailable.With("the runner did not start") : await Answer(process, inputJson, limits, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<Result<string>> Answer(Process process, string inputJson, CodeLimits limits, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(limits.Timeout);
        var output = Drain(process.StandardOutput.BaseStream, CodeLimits.MaxResultBytes, keepTail: false);
        var error = Drain(process.StandardError.BaseStream, CodeLimits.MaxStderrBytes, keepTail: true);
        try
        {
            await process.StandardInput.WriteAsync(inputJson.AsMemory(), deadline.Token).ConfigureAwait(false);
            process.StandardInput.Close();
            await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);   // the timeout or the caller: never leave the runner running
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var partial = StderrText(await error.ConfigureAwait(false));   // what the code wrote before the kill shows that it ran
            return SindriErrors.CodeTimeout.With($"the code ran past its limit of {limits.Timeout.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture)} s{(partial.Length == 0 ? string.Empty : ": " + partial)}");
        }
        catch (IOException)
        {
            // boundary: the runner ended before it read its input; its exit code says why
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        }

        var stdout = await output.ConfigureAwait(false);
        var message = StderrText(await error.ConfigureAwait(false));
        return process.ExitCode switch
        {
            0 when stdout.Cut => SindriErrors.CodeFailed.With("the result is larger than the cap of 1 MiB"),
            0 when stdout.Text.Length == 0 => SindriErrors.CodeFailed.With("the code ended without a result (did it call Environment.Exit?)"),
            0 => stdout.Text,
            2 => SindriErrors.CodeRunnerUnavailable.With(message),
            3 => SindriErrors.CodeMemory.With("the code ran out of memory"),
            4 => SindriErrors.CodeFailed.With(message),
            var other => SindriErrors.CodeFailed.With($"the runner ended with code {other.ToString(CultureInfo.InvariantCulture)}{(message.Length == 0 ? string.Empty : ": " + message)}"),
        };
    }

    private static string StderrText(Drained stderr)
    {
        var text = stderr.Text.Trim();
        return stderr.Cut ? "(stderr cut at 8 KiB: only the last part is kept) " + text : text;
    }

    // why: the pipe is read to its end even past the cap, so the child never blocks on a full pipe; only the cap's worth is kept
    private static async Task<Drained> Drain(Stream stream, int cap, bool keepTail)
    {
        var buffer = new byte[8192];
        var kept = new MemoryStream();
        var cut = false;
        int read;
        while ((read = await stream.ReadAsync(buffer, CancellationToken.None).ConfigureAwait(false)) > 0)
        {
            if (keepTail)
            {
                await kept.WriteAsync(buffer.AsMemory(0, read)).ConfigureAwait(false);
                if (kept.Length > 2L * cap)
                {
                    kept = Tail(kept, cap);
                    cut = true;
                }
            }
            else
            {
                var room = Math.Max(0, cap - (int)kept.Length);
                cut |= read > room;
                await kept.WriteAsync(buffer.AsMemory(0, Math.Min(read, room))).ConfigureAwait(false);
            }
        }

        if (keepTail && kept.Length > cap)
        {
            kept = Tail(kept, cap);
            cut = true;
        }

        return new Drained(System.Text.Encoding.UTF8.GetString(kept.GetBuffer(), 0, (int)kept.Length), cut);
    }

    private static MemoryStream Tail(MemoryStream source, int count)
    {
        var tail = new MemoryStream(count);
        tail.Write(source.GetBuffer(), (int)source.Length - count, count);
        return tail;
    }

    private sealed record Drained(string Text, bool Cut);
}
