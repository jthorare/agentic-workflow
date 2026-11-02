using Aw.Kernel;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Sandbox.Credentials;

/// <summary>Signs a target in to its own agent home ('sindri sandbox login', sandbox spec SB-8); the CLI verb's port, so its tests need no agent CLI.</summary>
public interface ISandboxLogin
{
    /// <summary>Makes the target's agent home (0700), installs the toolkit's hooks into it, then runs the target's interactive sign-in on this terminal.</summary>
    /// <param name="target">The agent: claude-code or codex.</param>
    /// <param name="cancellationToken">Stops waiting.</param>
    /// <returns>The sign-in's exit code, or the reason the home could not be made ready.</returns>
    public Task<Result<int>> Run(AgentId target, CancellationToken cancellationToken);
}
