using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Aw.Sindri.Infrastructure.Sessions;

/// <summary>
/// Runs ps and lsof (collector spec CI-3, ARCH-SESS-001): from the absolute path it is given, with TZ=UTC and LC_ALL=C so ps's times and lsof's fields read the same everywhere,
/// through its own bounded Process.Start that kills the child only. A tool that is missing, runs past its budget, is cancelled, fails
/// or prints more than the cap gives no answer; lsof's exit 1 with output (it could not read some of what it lists) is an answer.
/// </summary>
/// <param name="budget">The longest one call may run.</param>
internal sealed class MacToolRunner(TimeSpan budget) : IMacTools
{
    /// <summary>The longest one call runs in production: ps over a busy desktop answers in well under a second.</summary>
    internal static readonly TimeSpan CallBudget = TimeSpan.FromSeconds(5);

    /// <summary>How many characters of a tool's output are kept (16 MiB: ps -axww on a busy machine is under 1 MiB); output that reaches it is refused, because the start would be what is missing.</summary>
    internal const int OutputCharacterCap = 16 * 1024 * 1024;

    public string? Run(string tool, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(tool);
        ArgumentNullException.ThrowIfNull(arguments);
        var start = new ProcessStartInfo(tool) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        start.Environment["TZ"] = "UTC";
        start.Environment["LC_ALL"] = "C";
        Process? child;
        try
        {
            child = Process.Start(start);
        }
        catch (Win32Exception)
        {
            return null;   // not at its absolute path: never looked up by name
        }

        using (child)
        {
            return child is null ? null : Wait(child, cancellationToken);
        }
    }

    private string? Wait(Process child, CancellationToken cancellationToken)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(budget);
        // why: Register hands back the object passed in, which is this Process
        using var kill = limit.Token.Register(static state => Kill((Process)state!), child);
        child.ErrorDataReceived += static (_, _) => { };   // stderr is drained so a full pipe can't stall the child; its text isn't used
        child.BeginErrorReadLine();
        var output = Read(child.StandardOutput, child);
        child.WaitForExit();
        if (limit.IsCancellationRequested || output is null)
        {
            return null;
        }

        return child.ExitCode == 0 || (child.ExitCode == 1 && output.Length > 0) ? output : null;
    }

    private static string? Read(StreamReader reader, Process child)
    {
        var text = new StringBuilder();
        var chunk = new char[8192];
        int read;
        while ((read = reader.Read(chunk, 0, chunk.Length)) > 0)
        {
            _ = text.Append(chunk, 0, read);
            if (text.Length >= OutputCharacterCap)
            {
                Kill(child);
                return null;
            }
        }

        return text.ToString();
    }

    private static void Kill(Process child)
    {
        try
        {
            child.Kill();   // the child only
        }
        catch (InvalidOperationException)
        {
            // it exited between the deadline and the kill
        }
    }
}
