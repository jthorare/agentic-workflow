using System.ComponentModel;
using System.Diagnostics;

namespace Aw.Forge.Infrastructure.Git;

/// <summary>Runs one program with an exact argument list and an environment built from scratch plus what the caller adds.</summary>
internal static class ProcessRunner
{
    /// <summary>Environment variables kept from the parent: enough to find programs and a home, nothing git reads.</summary>
    private static readonly string[] _inherited = ["PATH", "HOME", "USERPROFILE", "SYSTEMROOT", "TMPDIR", "TEMP", "TMP", "LANG", "LC_ALL"];

    internal static async Task<ProcessResult?> Run(ProcessSpec spec, CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = StartInfo(spec) };
        if (!TryStart(process))
        {
            return null;
        }

        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var error = process.StandardError.ReadToEndAsync(CancellationToken.None);
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(spec.Timeout);
        var exitCode = -1;   // killed on the timeout
        try
        {
            await process.WaitForExitAsync(limit.Token).ConfigureAwait(false);
            exitCode = process.ExitCode;
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }

        return new ProcessResult(exitCode, await output.ConfigureAwait(false), await error.ConfigureAwait(false));
    }

    private static bool TryStart(Process process)
    {
        try
        {
            return process.Start();
        }
        catch (Win32Exception)
        {
            return false;
        }
    }

    private static ProcessStartInfo StartInfo(ProcessSpec spec)
    {
        var info = new ProcessStartInfo(spec.File)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in spec.Arguments)
        {
            info.ArgumentList.Add(argument);
        }

        if (spec.WorkingDirectory is not null)
        {
            info.WorkingDirectory = spec.WorkingDirectory;
        }

        info.Environment.Clear();
        foreach (var name in _inherited)
        {
            if (Environment.GetEnvironmentVariable(name) is { } value)
            {
                info.Environment[name] = value;
            }
        }

        foreach (var (name, value) in spec.Environment)
        {
            info.Environment[name] = value;
        }

        return info;
    }
}
