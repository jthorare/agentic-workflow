using Aw.Kernel;
using Aw.Meta.Core.Lanes;

namespace Aw.Meta.Core.Ports;

/// <summary>The lane records under lane-sessions: the session id a CLI's hook reported and the agent facts the launcher recorded.</summary>
public interface ILaneFiles
{
    /// <summary>Reads a lane's record.</summary>
    /// <param name="lane">The lane name.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The record, or null when the launcher left nothing for the lane. A file that can't be read counts as absent.</returns>
    public ValueTask<Result<LaneRecord?>> Record(string lane, CancellationToken cancellationToken);

    /// <summary>Reads the trace the lane's hooks left on their last run.</summary>
    /// <param name="lane">The lane name.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The trace; null when the file is missing or unreadable.</returns>
    public ValueTask<Result<LaneHookTrace?>> HookTrace(string lane, CancellationToken cancellationToken);

    /// <summary>Records the session id a lane now runs, the way the session-id hook does (atomically, owner-only), so a relaunch resumes it.</summary>
    /// <param name="lane">The lane name.</param>
    /// <param name="session">The session id.</param>
    /// <param name="at">When it was recorded (the in-memory fake keeps it; the directory adapter uses the file's own time).</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success, or AW-META-012 when the file can't be written or the lane name is not one.</returns>
    public ValueTask<Result<Unit>> RecordSession(string lane, Ids.SessionId session, DateTimeOffset at, CancellationToken cancellationToken);
}
