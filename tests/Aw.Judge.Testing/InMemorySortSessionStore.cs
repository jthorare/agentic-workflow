using Aw.Judge.Core.Errors;
using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.PromptSort;
using Aw.Kernel;

namespace Aw.Judge.Testing;

/// <summary>The in-memory ISortSessionStore fake; it passes SortSessionStoreContract.</summary>
public sealed class InMemorySortSessionStore : ISortSessionStore
{
    private static readonly Error _unavailable = new("AW-DATA-002", ErrorKind.Unavailable, "the in-memory session store is set unavailable");
    private readonly Dictionary<string, SortSession> _sessions = new(StringComparer.Ordinal);

    /// <summary>When true every write returns an error, as a sessions directory that can't be created.</summary>
    public bool Unavailable { get; init; }

    /// <summary>The sessions written so far, by session.</summary>
    public IReadOnlyDictionary<string, SortSession> Sessions => _sessions;

    /// <summary>Whether a session id can be a file name: letters, digits, '.', '_' and '-', at most 80, no '..'.</summary>
    /// <param name="session">The session.</param>
    /// <returns>True when it can.</returns>
    public static bool IsFileName(SessionId session) =>
        session.Value.Length is >= 1 and <= 80
        && session.Value.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-')
        && !session.Value.Contains("..", StringComparison.Ordinal);

    /// <inheritdoc />
    public ValueTask<Result<SortSession>> Read(SessionId session, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Results.Success(IsFileName(session) && _sessions.TryGetValue(session.Value, out var state) ? state : SortSession.Empty));

    /// <inheritdoc />
    public ValueTask<Result<Unit>> Write(SessionId session, SortSession state, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (Unavailable)
        {
            return ValueTask.FromResult(Results.Failure<Unit>(_unavailable));
        }

        if (!IsFileName(session))
        {
            return ValueTask.FromResult(Results.Failure<Unit>(JudgeErrors.Invalid.With($"session id can't be a file name: {session.Value}")));
        }

        _sessions[session.Value] = state;
        return ValueTask.FromResult(Results.Ok());
    }
}
