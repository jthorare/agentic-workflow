using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Testing;

/// <summary>Transcript modification times in memory, by session id.</summary>
public sealed class FakeTranscripts : ITranscripts
{
    private readonly Dictionary<string, DateTimeOffset> _writes = new(StringComparer.Ordinal);

    /// <summary>Sets the modification time of a session's transcript.</summary>
    /// <param name="session">The session id.</param>
    /// <param name="at">The time.</param>
    /// <returns>These transcripts.</returns>
    public FakeTranscripts With(string session, DateTimeOffset at)
    {
        _writes[session] = at;
        return this;
    }

    /// <inheritdoc/>
    public DateTimeOffset? LastWrite(AgentEntry agent, SessionId? session, string? hinted) =>
        session is { } id && _writes.TryGetValue(id.Value, out var at) ? at : null;
}
