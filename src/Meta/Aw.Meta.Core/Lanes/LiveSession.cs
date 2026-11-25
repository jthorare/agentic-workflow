using Aw.Meta.Core.Ids;

namespace Aw.Meta.Core.Lanes;

/// <summary>A running Claude session.</summary>
/// <param name="Session">The session id.</param>
/// <param name="Name">The session name; empty when it has none.</param>
/// <param name="Pid">The process id.</param>
/// <param name="Status">The status the session reports.</param>
/// <param name="Cwd">The session's working directory.</param>
public sealed record LiveSession(SessionId Session, string Name, int Pid, string Status, string Cwd)
{
    /// <summary>
    /// The sender address a message from this session carries ("uds:" and the session's messaging socket); null when the record names
    /// no socket. It stays the same when the session is renamed or cleared, because the process keeps its socket (#4103).
    /// </summary>
    public string? Address { get; init; }
}
