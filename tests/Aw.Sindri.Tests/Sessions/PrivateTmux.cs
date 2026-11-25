using System.Diagnostics;
using System.Globalization;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>
/// A tmux server of the test's own on the private socket name 'aw-co2b-&lt;pid&gt;' (tmux -L), with one session. It is started and killed here only: never the
/// default server, never a team's socket. Every child runs without TMUX and TMUX_PANE.
/// </summary>
internal sealed class PrivateTmux : IDisposable
{
    private readonly string _name;

    private PrivateTmux(string name, string socket, string pane)
    {
        _name = name;
        Socket = socket;
        Pane = pane;
    }

    /// <summary>The absolute path of the private server's socket.</summary>
    public string Socket { get; }

    /// <summary>The id ('%n') of the pane of the server's one session.</summary>
    public string Pane { get; }

    /// <summary>Starts the private server.</summary>
    /// <param name="tag">Makes the server's name its own, so two test classes never share (and kill) one server.</param>
    /// <returns>The server; null when tmux isn't installed or the server didn't start.</returns>
    public static PrivateTmux? Start(string tag = "")
    {
        var name = Name(tag);
        if (Run(name, "new-session", "-d", "-s", "co2b", "sleep 300") is null)
        {
            return null;
        }

        var socket = Run(name, "display-message", "-p", "#{socket_path}")?.Trim();
        var pane = Run(name, "display-message", "-p", "-t", "co2b", "#{pane_id}")?.Trim();
        if (socket is { Length: > 0 } && Path.IsPathRooted(socket) && pane is { Length: > 0 })
        {
            return new PrivateTmux(name, socket, pane);
        }

        _ = Run(name, "kill-server");
        RemoveSocket(socket);
        return null;
    }

    private static string Name(string tag) => string.Create(CultureInfo.InvariantCulture, $"aw-co2b-{Environment.ProcessId}") + (tag.Length == 0 ? string.Empty : "-" + tag);

    /// <summary>Opens a window that runs <paramref name="shellCommand"/> (through sh -c) and says which pane and process it is.</summary>
    /// <param name="shellCommand">The command.</param>
    /// <returns>The pane id and its first process; null when tmux refused.</returns>
    public (string Pane, int PanePid)? NewPane(string shellCommand)
    {
        var made = Run(_name, "new-window", "-d", "-P", "-F", "#{pane_id} #{pane_pid}", "-t", "co2b:", shellCommand)?.Trim().Split(' ');
        return made is [var pane, var pid] && int.TryParse(pid, CultureInfo.InvariantCulture, out var number) ? (pane, number) : null;
    }

    public void Dispose()
    {
        _ = Run(_name, "kill-server");   // this -L socket only
        RemoveSocket(Socket);
    }

    // tmux 3.4 leaves its socket file behind when the server exits, so each run left one aw-co2b-<pid> file in /tmp/tmux-<uid> (#2850).
    private static void RemoveSocket(string? socket)
    {
        if (socket is not { Length: > 0 } || !Path.IsPathRooted(socket) || !Path.GetFileName(socket).StartsWith("aw-co2b-", StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            File.Delete(socket);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string? Run(string name, params string[] arguments)
    {
        var start = new ProcessStartInfo("tmux", ["-L", name, .. arguments]) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        _ = start.Environment.Remove("TMUX");
        _ = start.Environment.Remove("TMUX_PANE");
        try
        {
            using var child = Process.Start(start);
            if (child is null)
            {
                return null;
            }

            var output = child.StandardOutput.ReadToEnd();
            _ = child.StandardError.ReadToEnd();
            child.WaitForExit();
            return child.ExitCode == 0 ? output : null;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }
}
