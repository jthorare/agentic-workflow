using System.Diagnostics;
using Aw.Kernel.Data.Processes;

namespace Aw.Context.Infrastructure.Extract;

/// <summary>Runs one command with exactly the environment given, bounded by a time limit, and kills its tree at the limit.</summary>
internal static class GraphifyProcess
{
    /// <summary>The most output characters kept from each stream.</summary>
    internal const int MaxKept = 64 * 1024;

    /// <summary>The most stderr characters kept (its end).</summary>
    internal const int TailKept = 4096;

    /// <summary>How a graphify process ended.</summary>
    /// <param name="Exit">The exit code; -1 when it timed out.</param>
    /// <param name="Stdout">What it printed (empty when it timed out).</param>
    /// <param name="TimedOut">True when it was killed at the time limit.</param>
    /// <param name="Stderr">The last 4096 characters it printed to stderr (the end, where a crash prints), for a failure message.</param>
    internal sealed record Ran(int Exit, string Stdout, bool TimedOut = false, string Stderr = "");

    /// <summary>
    /// Runs argv with exactly this environment; null when it can't start. A run that doesn't finish within <paramref name="limit"/> (null: no limit) is killed and answered as timed out.
    /// Output is drained, never logged.
    /// </summary>
    internal static async Task<Ran?> Spawn(IReadOnlyList<string> argv, Dictionary<string, string> environment, string directory, TimeSpan? limit, CancellationToken cancellationToken)
    {
        var start = StartInfo(argv, environment, directory);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(limit ?? Timeout.InfiniteTimeSpan);
        PinnedProcess? pinned = null;
        Process? process = null;
        try
        {
            // A thread that outlives this call forks the child: bwrap --die-with-parent dies with the thread that forked it, and pool threads retire (#1680).
            pinned = await PinnedProcess.StartAsync(start).ConfigureAwait(false);
            process = pinned.Process;
            var stdout = Capture(process.StandardOutput, timeout.Token);
            var stderr = CaptureTail(process.StandardError, timeout.Token);
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            var tail = await stderr.ConfigureAwait(false);
            return new Ran(process.ExitCode, await stdout.ConfigureAwait(false), Stderr: tail);
        }
        catch (OperationCanceledException)
        {
            Kill(process);
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            return new Ran(-1, string.Empty, TimedOut: true);
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
        finally
        {
            pinned?.Dispose();
        }
    }

    // Reads the stream to its end, keeping only the first MaxKept characters: a chatty tool can't grow this process.
    private static async Task<string> Capture(StreamReader reader, CancellationToken cancellationToken)
    {
        var kept = new System.Text.StringBuilder();
        var buffer = new char[4096];
        int read;
        while ((read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
        {
            _ = kept.Append(buffer, 0, Math.Min(read, MaxKept - kept.Length));
        }

        return kept.ToString();
    }

    // Reads the stream to its end, keeping only the last TailKept characters: a crash prints last, and a chatty tool can't grow this process.
    private static async Task<string> CaptureTail(StreamReader reader, CancellationToken cancellationToken)
    {
        var kept = new System.Text.StringBuilder();
        var buffer = new char[4096];
        int read;
        while ((read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
        {
            _ = kept.Append(buffer, 0, read);
            if (kept.Length > TailKept * 2)
            {
                _ = kept.Remove(0, kept.Length - TailKept);
            }
        }

        return kept.Length > TailKept ? kept.ToString(kept.Length - TailKept, TailKept) : kept.ToString();
    }

    private static ProcessStartInfo StartInfo(IReadOnlyList<string> argv, Dictionary<string, string> environment, string directory)
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

        return start;
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
