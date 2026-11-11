using System.Diagnostics;
using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Launch;

namespace Aw.Sindri.Tests.Launch;

/// <summary>
/// A tmux server no one else can reach (scripts/test-tmux.sh, in C#): its own TMUX_TMPDIR under a fresh temp directory and a
/// unique '-L' socket name, with TMUX removed from every child. Dispose kills only this server, then deletes the directory.
/// </summary>
internal sealed class PrivateTmux : IDisposable
{
    public string Socket { get; } = "t1-" + RealTime.NewGuid().ToString("N")[..12];

    public string Scratch { get; } = Directory.CreateTempSubdirectory("aw-tmux-").FullName;

    public string TmuxTmpDir => Path.Combine(Scratch, "tmux");

    public TmuxHostOptions Options => new("tmux", Socket, TmuxTmpDir);

    public static string? Tmux(string tmpDir, string socket, params string[] args) => Exec(tmpDir, socket, args).Output;

    /// <summary>Runs tmux and keeps why it failed: the exit code, stderr, and whether the 10 s wait ran out (issue 2832).</summary>
    public static TmuxResult Exec(string tmpDir, string socket, params string[] args)
    {
        using var process = Process.Start(Info(tmpDir, socket, args));
        if (process is null)
        {
            return new TmuxResult(Output: null, ExitCode: null, "tmux did not start", TimedOut: false);
        }

        // why: both pipes read at once, so a full stderr cannot block tmux while stdout is read to the end
        var ct = TestContext.Current.CancellationToken;
        var output = process.StandardOutput.ReadToEndAsync(ct);
        var error = process.StandardError.ReadToEndAsync(ct);
        if (!process.WaitForExit(10_000))
        {
            return new TmuxResult(Output: null, ExitCode: null, "tmux still running after 10 s", TimedOut: true);
        }

        // why: a detached server that kept the pipes would block the reads forever; say so instead of hanging
        if (!Task.WaitAll([output, error], 2_000, ct))
        {
            return new TmuxResult(Output: null, process.ExitCode, "tmux exited but its stdout or stderr stayed open (a detached server holds the pipe)", TimedOut: false);
        }

        var stdout = output.GetAwaiter().GetResult();
        var stderr = error.GetAwaiter().GetResult();
        return new TmuxResult(process.ExitCode == 0 ? stdout : null, process.ExitCode, stderr.Trim(), TimedOut: false);
    }

    public string? Run(params string[] args) => Tmux(TmuxTmpDir, Socket, args);

    public TmuxResult Exec(params string[] args) => Exec(TmuxTmpDir, Socket, args);

    public bool HasSession(string name) => Run("has-session", "-t", "=" + name) is not null;

    public Process Client(string name)
    {
        var info = Info(TmuxTmpDir, Socket, "-C", "attach-session", "-t", "=" + name);
        info.RedirectStandardInput = true;
        return Process.Start(info) ?? throw new InvalidOperationException("tmux did not start");
    }

    public void Dispose()
    {
        _ = Run("kill-server");
        Directory.Delete(Scratch, recursive: true);
    }

    private static ProcessStartInfo Info(string tmpDir, string socket, params string[] args)
    {
        Directory.CreateDirectory(tmpDir);
        var info = new ProcessStartInfo("tmux", ["-L", socket, .. args])
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        _ = info.Environment.Remove("TMUX");
        info.Environment["TMUX_TMPDIR"] = tmpDir;
        return info;
    }
}
