using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.Health;

/// <summary>
/// Reads systemd's DefaultOOMPolicy for the user manager (issue #2931). With "stop", one OOM kill inside a lane's tmux scope
/// stops the whole scope, agent included; "continue" kills only the process the kernel picked.
/// </summary>
/// <param name="runner">Runs systemctl --user show.</param>
internal sealed class OomPolicyCheck(ICommandRunner runner)
{
    /// <summary>The warning line when the policy is known and is not "continue".</summary>
    /// <returns>The line, or <see langword="null"/> when the policy is "continue" or can't be read (no systemctl, a failed or timed-out call: macOS too).</returns>
    internal ValueTask<string?> Warning(CancellationToken cancellationToken)
    {
        _ = runner;
        _ = cancellationToken;
        throw new NotSupportedException();
    }
}
