using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.SyncTodoNote;

/// <summary>The conflict picks made since the last sync: the note line each was resolved against, and the ones resolved against a removal.</summary>
/// <param name="Bases">The base line per todo.</param>
/// <param name="Removed">The todos whose pick was against a removal.</param>
internal sealed record PickedLines(IReadOnlyDictionary<TodoId, string> Bases, IReadOnlySet<TodoId> Removed)
{
    /// <summary>No picks.</summary>
    public static PickedLines None { get; } = new(new Dictionary<TodoId, string>(), new HashSet<TodoId>());
}
