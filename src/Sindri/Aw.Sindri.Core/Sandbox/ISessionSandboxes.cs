using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Sandbox;

/// <summary>Reads what a sandboxed session's plan file and cgroup scope say about it (sandbox spec §8); the files are the adapter's, Core bans System.IO.</summary>
public interface ISessionSandboxes
{
    /// <summary>Reads one launch's sandbox.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="summary">What 'launch.started' recorded: its presence is what makes the launch sandboxed, and it answers when the plan file does not read.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The sandbox; never an error: a file that is gone or unreadable is <see cref="SessionSandboxState.Unavailable"/>.</returns>
    public ValueTask<SessionSandbox> Read(LaunchId launch, SandboxSummary summary, CancellationToken cancellationToken);
}
