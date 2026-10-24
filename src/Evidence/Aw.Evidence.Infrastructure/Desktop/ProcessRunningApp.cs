using System.Diagnostics;
using System.Globalization;
using Aw.Evidence.Core.Desktop;

namespace Aw.Evidence.Infrastructure.Desktop;

/// <summary>An app running as a child process: its standard error is kept (the last lines) for the error that says why it ended; disposing kills it and what it started.</summary>
internal sealed class ProcessRunningApp : IRunningApp
{
    private const int KeptLines = 5;
    private static readonly TimeSpan _stopWait = TimeSpan.FromSeconds(5);

    private readonly Process _process;
    private readonly Queue<string> _tail = new();
    private bool _disposed;

    internal ProcessRunningApp(Process process, string display)
    {
        _process = process;
        Display = display;
        _process.ErrorDataReceived += (_, e) => Remember(e.Data);
        _process.OutputDataReceived += static (_, _) => { };   // drained so the app never blocks on a full pipe
        _process.BeginErrorReadLine();
        _process.BeginOutputReadLine();
    }

    /// <inheritdoc/>
    public string Display { get; }

    /// <inheritdoc/>
    public string? Ended()
    {
        if (_disposed)
        {
            return "was stopped";
        }

        if (!_process.HasExited)
        {
            return null;
        }

        string lines;
        lock (_tail)
        {
            lines = string.Join(" | ", _tail);
        }

        return string.Create(CultureInfo.InvariantCulture, $"exited {_process.ExitCode}: {lines}");
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }

            using var limit = new CancellationTokenSource(_stopWait);
            await _process.WaitForExitAsync(limit.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or OperationCanceledException)
        {
            // It already ended, or did not end in time; the system reaps it.
        }

        _disposed = true;
        _process.Dispose();
    }

    private void Remember(string? line)
    {
        if (line is null)
        {
            return;
        }

        lock (_tail)
        {
            _tail.Enqueue(line);
            while (_tail.Count > KeptLines)
            {
                _ = _tail.Dequeue();
            }
        }
    }
}
