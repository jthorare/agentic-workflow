using System.Collections.Concurrent;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Todos;

/// <summary>
/// The picks still inside their Undo window (todo spec D11, §4.4): a conflict pick per todo and at most one hold pick, each good for <see cref="Span"/> on Sindri's clock. Held in memory, one per process: a restart inside
/// the 10 seconds loses the Undo, which then answers SND-TODO-005 ("There's nothing to undo."). The note writer (todo Ph3b's sync PR) reads <see cref="Open"/> to leave a picked line alone until its window closes.
/// </summary>
public sealed class TodoPickWindow
{
    /// <summary>How long a pick can be undone.</summary>
    public static readonly TimeSpan Span = TimeSpan.FromSeconds(10);

    private readonly ConcurrentDictionary<TodoId, TodoPick> _picks = new();
    private TodoHoldPick? _hold;

    /// <summary>When a pick made at <paramref name="at"/> stops being undoable.</summary>
    /// <param name="at">When it was made.</param>
    /// <returns>The instant.</returns>
    public static DateTimeOffset Until(DateTimeOffset at) => at + Span;

    /// <summary>Remembers a conflict pick.</summary>
    /// <param name="id">The todo.</param>
    /// <param name="pick">The pick.</param>
    public void Remember(TodoId id, TodoPick pick)
    {
        ArgumentNullException.ThrowIfNull(pick);
        _picks[id] = pick;
    }

    /// <summary>The todo's conflict pick, when it is still inside its window.</summary>
    /// <param name="id">The todo.</param>
    /// <param name="now">The time now.</param>
    /// <returns>The pick, or null for none or an expired one.</returns>
    public TodoPick? Find(TodoId id, DateTimeOffset now) => _picks.TryGetValue(id, out var pick) && now < Until(pick.At) ? pick : null;

    /// <summary>Forgets the todo's conflict pick.</summary>
    /// <param name="id">The todo.</param>
    public void Forget(TodoId id) => _ = _picks.TryRemove(id, out _);

    /// <summary>Remembers a hold pick (it replaces any earlier one).</summary>
    /// <param name="pick">The pick.</param>
    public void RememberHold(TodoHoldPick pick)
    {
        ArgumentNullException.ThrowIfNull(pick);
        Volatile.Write(ref _hold, pick);
    }

    /// <summary>The hold pick, when it is still inside its window.</summary>
    /// <param name="now">The time now.</param>
    /// <returns>The pick, or null for none or an expired one.</returns>
    public TodoHoldPick? FindHold(DateTimeOffset now) => Volatile.Read(ref _hold) is { } pick && now < Until(pick.At) ? pick : null;

    /// <summary>Forgets the hold pick.</summary>
    public void ForgetHold() => Volatile.Write(ref _hold, null);

    /// <summary>The todos whose conflict pick is still inside its window, and whether the hold pick is.</summary>
    /// <param name="now">The time now.</param>
    /// <returns>The todo ids with an open pick, and whether a hold pick is open.</returns>
    public (IReadOnlyList<TodoId> Todos, bool Hold) Open(DateTimeOffset now) =>
        ([.. _picks.Where(p => now < Until(p.Value.At)).Select(static p => p.Key).OrderBy(static k => k.Value, StringComparer.Ordinal)], FindHold(now) is not null);
}
