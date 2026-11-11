using Aw.Kernel;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.Polling;

namespace Aw.Sindri.Testing.Connections;

/// <summary>An IEventPoller that answers from a script: each call gets the cursor it was asked for and its own call number (from 1), and the call is counted.</summary>
/// <param name="preset">The preset the poller serves ('linear').</param>
/// <param name="script">The answer for a cursor and a call number.</param>
public sealed class ScriptedEventPoller(string preset, Func<string?, int, Result<PollResult>> script) : IEventPoller
{
    private readonly List<string?> _cursors = [];

    /// <inheritdoc />
    public string Preset { get; } = preset;

    /// <summary>Gets how many times the provider was called.</summary>
    public int Calls => _cursors.Count;

    /// <summary>Gets the cursor each call was asked with, in order.</summary>
    public IReadOnlyList<string?> Cursors => _cursors;

    /// <inheritdoc />
    public ValueTask<Result<PollResult>> Poll(Connection connection, EventKind kind, string? cursor, CancellationToken cancellationToken)
    {
        _cursors.Add(cursor);
        return ValueTask.FromResult(script(cursor, _cursors.Count));
    }
}
