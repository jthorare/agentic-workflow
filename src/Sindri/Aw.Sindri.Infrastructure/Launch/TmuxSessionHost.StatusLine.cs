using System.Globalization;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox;

namespace Aw.Sindri.Infrastructure.Launch;

internal sealed partial class TmuxSessionHost
{
    /// <summary>
    /// Sets the session's own status line (sandbox spec §7), never the server's: the prefix is read once from this same server, after the session exists, because
    /// a server with no session has no options to read. It never fails the start: a session without its line is still sandboxed.
    /// </summary>
    private async ValueTask ShowStatusLine(HostSession session, HostRef name, CancellationToken deadline)
    {
        if (session.StatusRight is not { } template)
        {
            return;
        }

        var prefix = await Run(["show-options", "-gv", "prefix"], deadline).ConfigureAwait(false);
        var line = template.Replace(SandboxStatusLine.PrefixToken, SandboxStatusLine.PrefixLabel(prefix.ExitCode == 0 && prefix.Output.Length > 0 ? prefix.Output : "C-b"), StringComparison.Ordinal);
        _ = await Run(["set-option", "-t", OptionTarget(name), "status-right-length", Math.Max(line.Length + 1, 40).ToString(CultureInfo.InvariantCulture)], deadline).ConfigureAwait(false);   // why: tmux cuts the right side at 40 characters by default
        _ = await Run(["set-option", "-t", OptionTarget(name), "status-right", line], deadline).ConfigureAwait(false);
    }

    // why: tmux 3.4 reads an option's -t as a pane, so a bare '=name' is "no such session"; '=name:' is that exact session's current window.
    private static string OptionTarget(HostRef host) => Target(host) + ":";
}
