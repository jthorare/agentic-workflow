using System.Diagnostics;

namespace Aw.Spike.T3335;

/// <summary>The only place that starts tmux. Always `-L sindri-spike`, never an inherited TMUX. Stop = kill-server on that socket only.</summary>
internal static class SpikeTmux
{
    public const string Socket = "sindri-spike";

    public static ProcessStartInfo Start(IEnumerable<string> args)
    {
        var psi = new ProcessStartInfo("tmux")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.Environment.Remove("TMUX");
        psi.Environment.Remove("TMUX_PANE");
        psi.ArgumentList.Add("-L");
        psi.ArgumentList.Add(Socket);
        psi.ArgumentList.Add("-f");
        psi.ArgumentList.Add("/dev/null");
        foreach (var a in args)
        {
            psi.ArgumentList.Add(a);
        }
        return psi;
    }

    public static async Task<string> RunAsync(params string[] args)
    {
        using var p = Process.Start(Start(args)) ?? throw new InvalidOperationException("tmux did not start");
        var stdout = p.StandardOutput.ReadToEndAsync();
        var stderr = p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync().ConfigureAwait(false);
        await stderr.ConfigureAwait(false);
        return await stdout.ConfigureAwait(false);
    }

    public static async Task<bool> HasSessionAsync(string name)
    {
        using var p = Process.Start(Start(["has-session", "-t", name])) ?? throw new InvalidOperationException("tmux did not start");
        _ = await p.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
        await p.WaitForExitAsync().ConfigureAwait(false);
        return p.ExitCode == 0;
    }

    public static async Task NewSessionAsync(string name, int cols, int rows, string command)
    {
        if (!await HasSessionAsync(name).ConfigureAwait(false))
        {
            await RunAsync("new-session", "-d", "-s", name, "-x", cols.ToString(), "-y", rows.ToString(), command).ConfigureAwait(false);
        }
    }

    public static Task KillSessionAsync(string name) => RunAsync("kill-session", "-t", name);

    /// <summary>Stop only the spike server. Safe when none runs.</summary>
    public static void KillServer()
    {
        try
        {
            using var p = Process.Start(Start(["kill-server"]));
            p?.WaitForExit(5000);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"kill-server: {ex.Message}");
        }
    }
}
