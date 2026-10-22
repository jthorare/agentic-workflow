using System.Diagnostics;
using System.Text;

namespace Aw.CodeIndex.Infrastructure.Graph;

/// <summary>Runs one command with exactly the environment given, bounded by a time limit, and kills its tree at the limit. Output is drained and never kept past a cap, never logged.</summary>
internal static class GraphProcess
{
    private const int MaxKept = 64 * 1024;

    /// <summary>How a process ended.</summary>
    /// <param name="Exit">The exit code; -1 when it timed out.</param>
    /// <param name="Stdout">What it printed (at most 64 KB).</param>
    /// <param name="TimedOut">True when it was killed at the limit.</param>
    internal sealed record Ran(int Exit, string Stdout, bool TimedOut = false);

    /// <summary>Runs argv; null when it cannot start.</summary>
    /// <param name="argv">The command.</param>
    /// <param name="environment">The whole environment.</param>
    /// <param name="directory">The working directory.</param>
    /// <param name="limit">The time limit.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>How it ended, or null.</returns>
    internal static async Task<Ran?> Spawn(IReadOnlyList<string> argv, Dictionary<string, string> environment, string directory, TimeSpan limit, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(argv[0]) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, WorkingDirectory = directory };
        start.Environment.Clear();
        foreach (var (name, value) in environment)
        {
            start.Environment[name] = value;
        }

        foreach (var arg in argv.Skip(1))
        {
            start.ArgumentList.Add(arg);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(limit);
        Process? process = null;
        try
        {
            process = Process.Start(start) ?? throw new InvalidOperationException("the process did not start");
            var stdout = Capture(process.StandardOutput, timeout.Token);
            var stderr = Capture(process.StandardError, timeout.Token);
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            _ = await stderr.ConfigureAwait(false);
            return new Ran(process.ExitCode, await stdout.ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            Kill(process);
            return cancellationToken.IsCancellationRequested ? throw new OperationCanceledException(cancellationToken) : new Ran(-1, string.Empty, TimedOut: true);
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
        finally
        {
            process?.Dispose();
        }
    }

    private static async Task<string> Capture(StreamReader reader, CancellationToken cancellationToken)
    {
        var kept = new StringBuilder();
        var buffer = new char[4096];
        int read;
        while ((read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
        {
            _ = kept.Append(buffer, 0, Math.Min(read, MaxKept - kept.Length));
        }

        return kept.ToString();
    }

    private static void Kill(Process? process)
    {
        try
        {
            if (process is { HasExited: false })
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // it exited between the check and the kill
        }
    }
}
