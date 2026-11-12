using Aw.Meta.Core;
using Aw.Meta.Core.Commands;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.Health;

/// <summary>
/// Reads systemd's DefaultOOMPolicy for the user manager (issue #2931). With "stop", one OOM kill inside a lane's tmux scope
/// stops the whole scope, agent included; "continue" kills only the process the kernel picked.
/// </summary>
/// <param name="runner">Runs systemctl --user show.</param>
/// <param name="paths">Names the user config folder the installer writes the drop-in under.</param>
internal sealed class OomPolicyCheck(ICommandRunner runner, MetaPaths paths)
{
    /// <summary>The warning line when the policy is known and is not "continue".</summary>
    /// <returns>The line, or <see langword="null"/> when the policy is "continue" or can't be read (no systemctl, a failed or timed-out call: macOS too).</returns>
    internal async ValueTask<string?> Warning(CancellationToken cancellationToken)
    {
        // A wedged user bus must not hang health, so the call has its own timeout.
        var run = await runner.Run(new CommandRequest(["systemctl", "--user", "show", "-p", "DefaultOOMPolicy", "--value"], TimeSpan.FromSeconds(10)), cancellationToken).ConfigureAwait(false);
        if (run.IsError || run.Value.TimedOut || run.Value.ExitCode != 0)
        {
            return null;
        }

        var policy = run.Value.Stdout.Split('\n', 2)[0].Trim();
        return policy.Length == 0 || policy == "continue"
            ? null
            : $"systemd's DefaultOOMPolicy is {policy}: one OOM kill in a pane stops the whole pane, agent included. Fix: re-run install-meta.sh from the toolkit checkout (it writes {DropIn()}), then systemctl --user daemon-reload";
    }

    private string DropIn()
    {
        var config = !string.IsNullOrEmpty(paths.UserConfig)
            ? paths.UserConfig
            : !string.IsNullOrEmpty(paths.Home) ? Path.Combine(paths.Home, ".config") : "~/.config";
        return Path.Combine(config, "systemd", "user.conf.d", "10-oom-continue.conf");
    }
}
