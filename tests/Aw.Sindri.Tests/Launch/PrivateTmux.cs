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

    public static string? Tmux(string tmpDir, string socket, params string[] args)
    {
        using var process = Process.Start(Info(tmpDir, socket, args));
        if (process is null)
        {
            return null;
        }

        var output = process.StandardOutput.ReadToEnd();
        _ = process.StandardError.ReadToEnd();
        return process.WaitForExit(10_000) && process.ExitCode == 0 ? output : null;
    }

    public string? Run(params string[] args) => Tmux(TmuxTmpDir, Socket, args);

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
