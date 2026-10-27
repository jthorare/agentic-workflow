using System.Globalization;
using System.Text;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;

#pragma warning disable MA0182 // until the sandbox launcher (SB-5a) starts it
namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>
/// The inside half of §3.2: the init shell that starts both forwarders, then runs the agent as its child (the sandbox has no
/// PID namespace, so an <c lang="sh">exec</c> would leave the forwarders running after the agent; sandbox spec §15 item 11 and the
/// SB-4a review). The self-check of the spec's start order is a second run of this same script with a check argv.
/// </summary>
internal static class EgressInit
{
    /// <summary>Where the net directory is bound inside the sandbox.</summary>
    public const string InsideDirectory = "/run/sindri/net";

    /// <summary>The loopback names the proxy must never see.</summary>
    public const string NoProxy = "127.0.0.1,localhost,::1";

    /// <summary>The init's exit status when a forwarder died at start; the launcher reads it as -016 net-forward.</summary>
    public const int ForwarderExitCode = 70;

    /// <summary>The -016 net-forward refusal for a forwarder that died at start.</summary>
    /// <param name="detail">What happened, already scrubbed.</param>
    /// <returns>The error.</returns>
    public static Error ForwarderFailed(string detail) =>
        SandboxRefusal.For(SindriErrors.LaunchSelfCheckFailed, SandboxReasons.NetForward, detail);

    /// <summary>
    /// Renders the init script. Its arguments are the agent's argv, which must not be empty. A TERM, INT or HUP to the init reaches the
    /// agent, the init waits for it, then stops both forwarders; the agent's exit status is the init's. The agent keeps the init's standard input (held on
    /// fd 3 because a shell without job control gives a background command <c lang="sh">/dev/null</c>). Needs a POSIX <c lang="sh">sh</c>; on Linux it reads <c lang="sh">/proc</c>
    /// to tell a dead forwarder from a zombie. The agent starts with SIGINT and SIGQUIT ignored (a POSIX rule for background commands); the launcher sends TERM. A forwarder that is not running
    /// shortly after start ends the init with <see cref="ForwarderExitCode"/> before the agent runs.
    /// </summary>
    /// <param name="network">The plan's network.</param>
    /// <param name="socat">The socat to run (a name or path).</param>
    /// <param name="socatLog">The file both forwarders append their output to (a path inside the sandbox).</param>
    /// <param name="insideDirectory">Where the net directory is bound; the sandbox's own path unless a test runs the script outside one.</param>
    /// <returns>The script text.</returns>
    public static string Render(PlanNetwork network, string socat, string socatLog, string insideDirectory = InsideDirectory)
    {
        ArgumentNullException.ThrowIfNull(network);
        ArgumentNullException.ThrowIfNull(socat);
        ArgumentNullException.ThrowIfNull(socatLog);
        ArgumentNullException.ThrowIfNull(insideDirectory);
        var script = new StringBuilder();
        _ = script.Append("#!/bin/sh\n")
            .Append("# Sindri sandbox init (sandbox spec 3.2): start both forwarders, then run the agent as a child.\n")
            .Append("exec 3>&- 4>&- 5>&- 6>&- 7>&- 8>&- 9>&-\n")
            .Append("alive() { kill -0 \"$1\" 2>/dev/null || return 1; [ -r \"/proc/$1/stat\" ] || return 0; read -r l < \"/proc/$1/stat\"; l=${l##*) }; [ \"${l%% *}\" != Z ]; }\n")
            .Append(Forward(socat, socatLog, network.ProxyPort, insideDirectory + "/proxy.sock")).Append("proxy=$!\n")
            .Append(Forward(socat, socatLog, network.SindriPort, insideDirectory + "/sindri.sock")).Append("sindri=$!\n")
            .Append("trap 'kill \"$proxy\" \"$sindri\" 2>/dev/null' EXIT\n")
            .Append("trap 'exit 143' TERM INT HUP\n")
            .Append("exec 3<&0 || exec 3</dev/null\n")
            .Append("sleep 0.2\n")
            .Append("alive \"$proxy\" && alive \"$sindri\" || { echo 'sindri: a network forwarder is not running; see ").Append(socatLog.Replace("'", "", StringComparison.Ordinal)).Append("' >&2; exit ").Append(ForwarderExitCode.ToString(CultureInfo.InvariantCulture)).Append("; }\n")
            .Append("export NO_PROXY='").Append(NoProxy).Append("' no_proxy='").Append(NoProxy).Append("'\n")
            .Append("\"$@\" <&3 3<&- &\n")
            .Append("child=$!\n")
            .Append("trap 'kill \"$child\" 2>/dev/null' TERM INT HUP\n")
            .Append("while :; do\n")
            .Append("  wait \"$child\"\n")
            .Append("  status=$?\n")
            .Append("  kill -0 \"$child\" 2>/dev/null || break\n")
            .Append("done\n")
            .Append("exit \"$status\"\n");
        return script.ToString();
    }

    private static string Forward(string socat, string log, int port, string socket) =>
        string.Create(CultureInfo.InvariantCulture, $"{Quote(socat)} TCP-LISTEN:{port},bind=127.0.0.1,fork,reuseaddr UNIX-CONNECT:{socket} >>{Quote(log)} 2>&1 &\n");

    private static string Quote(string text) => "'" + text.Replace("'", "'\\''", StringComparison.Ordinal) + "'";
}
#pragma warning restore MA0182
