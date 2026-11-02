using System.Diagnostics;
using System.Runtime.ExceptionServices;

namespace Aw.Kernel.Data.Processes;

/// <summary>
/// A started child process whose forking thread stays alive until this is disposed. A child that asked for PR_SET_PDEATHSIG (bwrap --die-with-parent) is killed
/// when the thread that forked it exits, and pool threads retire after about 20 s idle; keep the wrapper until you are done with the process.
/// </summary>
public sealed class PinnedProcess : IDisposable
{
    private readonly ManualResetEventSlim _release;
    private int _disposed;

    private PinnedProcess(Process process, ManualResetEventSlim release)
    {
        Process = process;
        _release = release;
    }

    /// <summary>The child process.</summary>
    public Process Process { get; }

    /// <summary>Starts the process from a dedicated thread that lives until the result is disposed.</summary>
    /// <param name="start">What to start.</param>
    /// <returns>The pinned process; a start failure (InvalidOperationException, Win32Exception) surfaces with its own type.</returns>
    public static Task<PinnedProcess> StartAsync(ProcessStartInfo start)
    {
        ArgumentNullException.ThrowIfNull(start);
        var source = new TaskCompletionSource<PinnedProcess>(TaskCreationOptions.RunContinuationsAsynchronously);
        Launch(start, started: p => source.TrySetResult(p), failed: e => source.TrySetException(e));
        return source.Task;
    }

    /// <summary>Starts the process from a dedicated thread that lives until the result is disposed; waits for the start (for short calls).</summary>
    /// <param name="start">What to start.</param>
    /// <returns>The pinned process; a start failure (InvalidOperationException, Win32Exception) is thrown with its own type.</returns>
    public static PinnedProcess Start(ProcessStartInfo start)
    {
        ArgumentNullException.ThrowIfNull(start);
        PinnedProcess? pinned = null;
        Exception? failure = null;
        using var signal = new ManualResetEventSlim(initialState: false);
        Launch(start, started: p => { pinned = p; signal.Set(); }, failed: e => { failure = e; signal.Set(); });
        signal.Wait();
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }

        // why: Launch calls started or failed before the signal fires, and failed throws above.
        return pinned!;
    }

    /// <summary>Lets the forking thread end and disposes the process. Safe to call more than once.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _release.Set();
            Process.Dispose();
        }
    }

    private static void Launch(ProcessStartInfo start, Action<PinnedProcess> started, Action<Exception> failed)
    {
        var thread = new Thread(() =>
        {
            using var release = new ManualResetEventSlim(initialState: false);
            try
            {
                var process = Process.Start(start) ?? throw new InvalidOperationException("the process didn't start");
                started(new PinnedProcess(process: process, release: release));
            }
            catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                failed(e);
                return;
            }

            release.Wait();
        })
        { IsBackground = true, Name = "pinned-process" };
        thread.Start();
    }
}
