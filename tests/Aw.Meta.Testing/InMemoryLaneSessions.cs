using Aw.Kernel;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Testing;

/// <summary>The in-memory ILaneSessions: sessions are scripted per test, or changed by a hook when a test needs a /clear to take effect.</summary>
public sealed class InMemoryLaneSessions : ILaneSessions
{
    private readonly Dictionary<string, SessionId> _current = new(StringComparer.Ordinal);

    /// <summary>How many times the current id was asked for.</summary>
    public int Calls { get; private set; }

    /// <summary>Starts a Claude in a lane's pane the way the real adapter finds it: its sessions record holds the id; one that can't be read gives none.</summary>
    /// <param name="session">The tmux session name.</param>
    /// <param name="depth">How far below the pane's own process the Claude runs (the fake does not need it).</param>
    /// <param name="record">The content of the Claude's sessions record.</param>
    public void Start(string session, int depth, string record)
    {
        _ = depth;
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(record);
            if (document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object
                && document.RootElement.TryGetProperty("sessionId", out var id) && id.ValueKind == System.Text.Json.JsonValueKind.String && id.GetString() is { Length: > 0 } text)
            {
                Set(session, text);
            }
        }
        catch (System.Text.Json.JsonException)
        {
            // an unreadable record gives no id
        }
    }

    /// <summary>Sets the id the lane's Claude runs now.</summary>
    /// <param name="session">The tmux session name.</param>
    /// <param name="id">The session id.</param>
    public void Set(string session, string id) => _current[session] = SessionId.From(id);

    /// <summary>Ends the lane's Claude: it reports no session id any more.</summary>
    /// <param name="session">The tmux session name.</param>
    public void End(string session) => _current.Remove(session);

    /// <inheritdoc />
    public ValueTask<Result<SessionId?>> Current(string session, CancellationToken cancellationToken)
    {
        Calls++;
        return Done.Of<SessionId?>(_current.TryGetValue(session, out var id) ? id : null);
    }
}
