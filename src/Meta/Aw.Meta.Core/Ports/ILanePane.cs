using Aw.Kernel;
using Aw.Meta.Core.Lanes;

namespace Aw.Meta.Core.Ports;

/// <summary>A lane's tmux session: what runs in it, who is attached, and the one wake line.</summary>
public interface ILanePane
{
    /// <summary>Looks at the session.</summary>
    /// <param name="session">The session name ("team-lane"); targeted exactly.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The pane's state, or null when there is no such session (or tmux is not available).</returns>
    public ValueTask<Result<PaneState?>> Probe(string session, CancellationToken cancellationToken);

    /// <summary>Reads the pid of the process the session's pane started (tmux's pane_pid).</summary>
    /// <param name="session">The session name ("team-lane"); targeted exactly.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The pid, or null when there is no such session (or tmux is not available).</returns>
    public ValueTask<Result<int?>> PanePid(string session, CancellationToken cancellationToken);

    /// <summary>Types text into the session's pane without pressing Enter: literal mode, exact target.</summary>
    /// <param name="session">The session name.</param>
    /// <param name="text">The text, sent literally.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success, or the failure to run tmux.</returns>
    public ValueTask<Result<Unit>> Type(string session, string text, CancellationToken cancellationToken);

    /// <summary>Presses Enter in the session's pane.</summary>
    /// <param name="session">The session name.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success, or the failure to run tmux.</returns>
    public ValueTask<Result<Unit>> PressEnter(string session, CancellationToken cancellationToken);

    /// <summary>Clears the input line of the session's pane (Ctrl+U), so typed text that must not be submitted doesn't sit there for the next send (#3521).</summary>
    /// <param name="session">The session name.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success, or the failure to run tmux.</returns>
    public ValueTask<Result<Unit>> ClearInput(string session, CancellationToken cancellationToken);

    /// <summary>Reads what the session's pane shows now (tmux capture-pane -p).</summary>
    /// <param name="session">The session name.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The visible text, or the failure to run tmux.</returns>
    public ValueTask<Result<string>> Capture(string session, CancellationToken cancellationToken);

    /// <summary>Types one line into the session's pane and presses Enter: literal mode, exact target.</summary>
    /// <param name="session">The session name.</param>
    /// <param name="line">The line, sent literally.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success, or the failure to run tmux.</returns>
    public ValueTask<Result<Unit>> Wake(string session, string line, CancellationToken cancellationToken);
}
