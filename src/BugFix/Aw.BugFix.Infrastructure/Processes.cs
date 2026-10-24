using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Aw.BugFix.Infrastructure;

/// <summary>Runs a program to completion within a time limit, capturing its output or streaming it to a log.</summary>
internal static class Processes
{
    /// <summary>The exit code reported for a program that couldn't start.</summary>
    internal const int CouldNotStart = 127;

    /// <summary>The exit code reported for a program killed at its time limit.</summary>
    internal const int TimedOut = 124;

    /// <summary>The most output kept or logged per run; the rest is dropped and the log says so.</summary>
    internal const int OutputCap = 16 * 1024 * 1024;

    // How long output is still read after the program exits: background children that keep the pipes open don't hold
    // the verdict hostage, and don't turn a finished run into a timed-out one.
    private static readonly TimeSpan _drainAfterExit = TimeSpan.FromSeconds(5);

    internal static ProcessResult Capture(ProcessStartInfo start, TimeSpan timeout, string? input = null)
    {
        using var output = new MemoryStream();
        using var error = new MemoryStream();
        var (code, _, note) = Run(start, timeout, output, error, input);
        return new ProcessResult(code, Encoding.UTF8.GetString(output.ToArray()), note ?? Encoding.UTF8.GetString(error.ToArray()));
    }

    /// <summary>Runs with stdout and stderr appended to <paramref name="log"/>; Ran is false when it couldn't start or hit the time limit.</summary>
    internal static (int Code, bool Ran) ToLog(ProcessStartInfo start, TimeSpan timeout, string log)
    {
        using var file = new FileStream(log, FileMode.Create, FileAccess.Write, FileShare.Read);
        using var sink = Stream.Synchronized(file);
        var (code, ran, note) = Run(start, timeout, sink, sink, input: null);
        if (note is not null)
        {
            sink.Write(Encoding.UTF8.GetBytes($"\nbugfix-state: {note}\n"));
        }

        return (code, ran);
    }

    internal static ProcessStartInfo Start(string program, IEnumerable<string> args, string? directory = null)
    {
        var start = new ProcessStartInfo(program) { WorkingDirectory = directory ?? string.Empty };
        foreach (var arg in args)
        {
            start.ArgumentList.Add(arg);
        }

        return start;
    }

    // The deadline covers the process; after it exits, output is drained only briefly, so a background child holding the
    // pipes neither stretches the run nor erases its exit code (the child itself left the process tree and isn't killed).
    private static (int Code, bool Ran, string? Note) Run(ProcessStartInfo start, TimeSpan timeout, Stream output, Stream error, string? input)
    {
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        start.RedirectStandardInput = true;
        start.UseShellExecute = false;
        Process process;
        try
        {
            process = Process.Start(start)!; // why: Start returns null only when UseShellExecute reuses a process; it is off.
        }
        catch (Win32Exception ex)
        {
            return (CouldNotStart, false, $"{start.FileName} can't run: {ex.Message}");
        }

        using (process)
        {
            var pumps = Task.WhenAll(Pump(process.StandardOutput.BaseStream, output), Pump(process.StandardError.BaseStream, error));
            WriteInput(process, input);
            if (!process.WaitForExit(timeout))
            {
                Kill(process);
                return (TimedOut, false, $"{start.FileName} took longer than {timeout}; killed and counted as not run");
            }

#pragma warning disable RS0030 // bugfix-state is a synchronous CLI; the wait is bounded by _drainAfterExit
            return pumps.Wait(_drainAfterExit)
                ? (process.ExitCode, true, null)
                : (process.ExitCode, true, string.Create(CultureInfo.InvariantCulture, $"{start.FileName} exited {process.ExitCode} but left processes holding its output; output after the exit was not kept"));
#pragma warning restore RS0030
        }
    }

    // The program reads its input while the pumps drain its output; a program that exits without reading it is not an error here.
    private static void WriteInput(Process process, string? input)
    {
        try
        {
            if (input is not null)
            {
                process.StandardInput.Write(input);
            }

            process.StandardInput.Close();
        }
        catch (IOException)
        {
            // the program closed its input early.
        }
    }

    private static async Task Pump(Stream from, Stream to)
    {
        var buffer = new byte[81920];
        var written = 0L;
        int read;
        while ((read = await from.ReadAsync(buffer).ConfigureAwait(false)) > 0)
        {
            var keep = (int)Math.Min(read, Math.Max(0, OutputCap - written));
            if (keep > 0)
            {
                await to.WriteAsync(buffer.AsMemory(0, keep)).ConfigureAwait(false);
                written += keep;
            }
        }
    }

    private static void Kill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // already exited: its descendants hold the pipes, and nothing more can be done from here.
        }
    }
}
