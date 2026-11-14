using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Features.GetSessionPreview;

/// <summary>
/// The question of an asking session (Agents page spec §7, issue 3082): the last 3 non-empty lines of its tmux pane, read when a request asks and never stored, logged,
/// recorded or put in an event. Null for a session that is not asking, is not in tmux, or whose pane can't be read.
/// </summary>
/// <param name="captures">The shared pane reads.</param>
/// <param name="processes">The process table, for the liveness check.</param>
internal sealed class SessionQuestions(PaneCaptures captures, IProcessTable processes)
{
    /// <summary>The question of <paramref name="session"/>, or null.</summary>
    /// <param name="session">The session.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The text, or null.</returns>
    internal async ValueTask<string?> Of(ObservedSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.State != SessionState.Asking || session.Host is not { Kind: "tmux", Tmux: { Socket: { } socket, Pane: { } pane } })
        {
            return null;
        }

        var read = await captures.Share(socket, pane).WaitAsync(cancellationToken).ConfigureAwait(false);
        if (read.Kind != PaneReadKind.Captured || !InPane(session, read.PanePid, cancellationToken))
        {
            return null;   // timed out, gone or another process's pane: no question, never an error
        }

        return PaneQuestion.Of(PreviewRules.Of(read.Capture.Span).Lines);
    }

    private bool InPane(ObservedSession session, int panePid, CancellationToken cancellationToken)
    {
        var table = processes.Read(cancellationToken);
        return table.IsSuccess && PreviewRules.InPane(session.Pid, session.StartTicks, panePid, table.Value);
    }
}
