using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Launch;

/// <summary>Types one line into a launched Step session's prompt (the idle sweep's one re-prompt); the tmux host can, the others can't.</summary>
public interface ISessionNudges
{
    /// <summary>Types <paramref name="text"/> and Enter into the launch's session.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="text">One line of text.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>True when the host took it; false when the session is gone, the host can't type into it or could not be asked.</returns>
    public ValueTask<bool> Send(LaunchId launch, string text, CancellationToken cancellationToken);
}
