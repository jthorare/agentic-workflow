using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Todos;

/// <summary>One todo as GET /sindri/todos serves it (todo spec §4.2): the stored row without its internal fields, links described.</summary>
/// <param name="Id">The todo's id.</param>
/// <param name="Text">The text, scrubbed.</param>
/// <param name="Due">The due day, or null.</param>
/// <param name="Links">The links.</param>
/// <param name="AddedAt">When it was added.</param>
/// <param name="AddedBy">Who added it.</param>
/// <param name="AddedVia">The 'via' of the add, or null.</param>
/// <param name="DoneAt">When it was checked off, or null.</param>
/// <param name="Removed">Whether it is removed.</param>
/// <param name="RemovedAt">When it was removed, or null.</param>
/// <param name="RemovedBy">Who removed it, or null.</param>
/// <param name="Version">The version a write sends back.</param>
/// <param name="LastBy">Who wrote last.</param>
/// <param name="LastVia">The 'via' of the last write, or null.</param>
/// <param name="Conflict">The open both-places conflict, or null.</param>
public sealed record TodoItem(
    TodoId Id,
    string Text,
    DateOnly? Due,
    IReadOnlyList<TodoLinkItem> Links,
    DateTimeOffset AddedAt,
    string AddedBy,
    string? AddedVia,
    DateTimeOffset? DoneAt,
    bool Removed,
    DateTimeOffset? RemovedAt,
    string? RemovedBy,
    long Version,
    string LastBy,
    string? LastVia,
    TodoOpenConflict? Conflict)
{
    /// <summary>The wire form of a stored row.</summary>
    /// <param name="row">The row.</param>
    /// <returns>The item.</returns>
    public static TodoItem From(TodoRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return new TodoItem(
            row.Id,
            row.Text,
            row.Due,
            [.. row.Links.Select(static l => TodoLinks.Describe(l)).Select(static d => new TodoLinkItem(d.Kind, d.Ref, d.Href))],
            row.AddedAt,
            row.AddedBy,
            row.AddedVia,
            row.DoneAt,
            row.Removed,
            row.RemovedAt,
            row.RemovedBy,
            row.Version,
            row.LastBy,
            row.LastVia,
            row.Conflict);
    }
}
