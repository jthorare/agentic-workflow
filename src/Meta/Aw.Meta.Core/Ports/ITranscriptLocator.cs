using Aw.Kernel;
using Aw.Meta.Core.Ids;

namespace Aw.Meta.Core.Ports;

/// <summary>Finds the file of a Claude Code session's transcript by the session id alone, with no offsets row needed.</summary>
public interface ITranscriptLocator
{
    /// <summary>Finds a session's transcript file.</summary>
    /// <param name="session">The session id.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The path, or null when no project holds a transcript for the session.</returns>
    public ValueTask<Result<string?>> Find(SessionId session, CancellationToken cancellationToken);
}
