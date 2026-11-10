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

    /// <summary>Reads the detached recycle waiter recorded for a lane.</summary>
    /// <param name="lane">The lane name.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The waiter's pid and when it was recorded; null when none is recorded.</returns>
    public ValueTask<Result<(int Pid, DateTimeOffset StartedAt)?>> Waiter(string lane, CancellationToken cancellationToken);

    /// <summary>Records the detached recycle waiter of a lane, or clears the record.</summary>
    /// <param name="lane">The lane name.</param>
    /// <param name="waiter">The waiter's pid and start time; null clears the record.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success.</returns>
    public ValueTask<Result<Unit>> RecordWaiter(string lane, (int Pid, DateTimeOffset StartedAt)? waiter, CancellationToken cancellationToken);
}
