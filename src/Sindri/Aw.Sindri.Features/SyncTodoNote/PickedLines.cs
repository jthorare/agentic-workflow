using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.SyncTodoNote;

/// <summary>The picks made since the last sync: the note line each conflict was resolved against, the ones resolved against a removal, and the todos of a hold the owner put back.</summary>
/// <param name="Bases">The base line per todo.</param>
/// <param name="Removed">The todos whose pick was against a removal.</param>
internal sealed record PickedLines(IReadOnlyDictionary<TodoId, string> Bases, IReadOnlySet<TodoId> Removed)
{
    /// <summary>Gets the todos a "Put them back" of the hold named; they are left alone while that pick can be undone.</summary>
    public IReadOnlyList<TodoId> Held { get; init; } = [];

    /// <summary>No picks.</summary>
    public static PickedLines None { get; } = new(new Dictionary<TodoId, string>(), new HashSet<TodoId>());
}
