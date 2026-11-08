using Aw.Kernel;
using Aw.Meta.Core.Ids;

namespace Aw.Meta.Core.Ports;

/// <summary>Finds a Codex session's rollout file (~/.codex/sessions/&lt;y&gt;/&lt;m&gt;/&lt;d&gt;/rollout-*-&lt;session&gt;.jsonl) and reads only its end.</summary>
public interface ICodexRollouts
{
    /// <summary>Reads the tail of a session's rollout.</summary>
    /// <param name="session">The session id.</param>
    /// <param name="maxBytes">How many bytes from the end to read at most.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The tail, or null when no rollout exists for the session; AW-META-004 when the file can't be read.</returns>
    public ValueTask<Result<TranscriptTail?>> Tail(SessionId session, int maxBytes, CancellationToken cancellationToken);
}
