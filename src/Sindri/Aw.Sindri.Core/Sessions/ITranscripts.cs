using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Sessions;

/// <summary>Transcript files, only ever stat'ed (collector spec §5.2, §5.3): the modification time is all the collector learns of one.</summary>
public interface ITranscripts
{
    /// <summary>When the session's transcript last changed.</summary>
    /// <param name="agent">The agent (its catalog 'transcripts' glob).</param>
    /// <param name="session">The session id, when known.</param>
    /// <param name="hinted">A path the state file or an open file link gave, or null.</param>
    /// <returns>The modification time, or null when there is no transcript.</returns>
    public DateTimeOffset? LastWrite(AgentEntry agent, SessionId? session, string? hinted);
}
