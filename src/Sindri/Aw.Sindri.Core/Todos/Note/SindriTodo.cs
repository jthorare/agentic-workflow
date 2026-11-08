
namespace Aw.Sindri.Core.Todos.Note;

/// <summary>A todo as the Sindri side of the merge sees it: a plain record to build from the todo projection (todo spec §4.2).</summary>
/// <param name="Id">The todo's id, 'td_' plus 26 lowercase ULID characters.</param>
/// <param name="Text">The scrubbed text.</param>
/// <param name="Due">The due date, or null.</param>
/// <param name="Links">The deep links ('sindri://…'), at most 10.</param>
/// <param name="Done">True once checked off.</param>
/// <param name="DoneOn">The day it was checked off, or null.</param>
/// <param name="Removed">True once removed.</param>
/// <param name="RemovedBy">Who removed it, or null while it isn't removed.</param>
/// <param name="LastLineHash">The <see cref="NoteLineHash"/> of the note line it had when removed, or null.</param>
/// <param name="Version">The todo's version.</param>
public sealed record SindriTodo(
    TodoId Id,
    string Text,
    DateOnly? Due,
    IReadOnlyList<string> Links,
    bool Done = false,
    DateOnly? DoneOn = null,
    bool Removed = false,
    NoteOrigin? RemovedBy = null,
    string? LastLineHash = null,
    long Version = 1);
