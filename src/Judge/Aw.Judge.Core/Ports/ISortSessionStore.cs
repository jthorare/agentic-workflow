using Aw.Judge.Core.Ids;
using Aw.Judge.Core.PromptSort;
using Aw.Kernel;

namespace Aw.Judge.Core.Ports;

/// <summary>Where a session's sorter state lives ('judge/sessions/&lt;sid&gt;.sort.json'). Every call returns an error, never throws.</summary>
public interface ISortSessionStore
{
    /// <summary>Reads a session's state.</summary>
    /// <param name="session">The session.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The state; <see cref="SortSession.Empty"/> when there is none, the file isn't readable as one, or the session id can't be a file name.</returns>
    public ValueTask<Result<SortSession>> Read(SessionId session, CancellationToken cancellationToken);

    /// <summary>
    /// Writes a session's state, keeping any key of the file this build doesn't know and replacing the file whole (a
    /// temp file and a rename), so done-gate.sh never reads half a file.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="state">The state.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Success, or why it can't be written (the session id can't be a file name: letters, digits, '.', '_' and '-', at most 80).</returns>
    public ValueTask<Result<Unit>> Write(SessionId session, SortSession state, CancellationToken cancellationToken);
}
