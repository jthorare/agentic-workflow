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
        var output = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var error = process.StandardError.ReadToEndAsync(CancellationToken.None);
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
            return SindriErrors.CodeTimeout.With($"the code ran past its limit of {limits.Timeout.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture)} s");
        }
        catch (IOException)
        {
            // boundary: the runner ended before it read its input; its exit code says why
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        }

        var stdout = await output.ConfigureAwait(false);
        var message = (await error.ConfigureAwait(false)).Trim();
        return process.ExitCode switch
        {
            0 when stdout.Length == 0 => SindriErrors.CodeFailed.With("the code ended without a result (did it call Environment.Exit?)"),
            0 => stdout,
            2 => SindriErrors.CodeRunnerUnavailable.With(message),
            3 => SindriErrors.CodeMemory.With("the code ran out of memory"),
            4 => SindriErrors.CodeFailed.With(message),
            var other => SindriErrors.CodeFailed.With($"the runner ended with code {other.ToString(CultureInfo.InvariantCulture)}{(message.Length == 0 ? string.Empty : ": " + message)}"),
        };
    }
}
