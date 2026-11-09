using System.Security.Cryptography;
using System.Text;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Todos;
using Aw.Sindri.Core.Todos.Note;

namespace Aw.Sindri.Features.SyncTodoNote;

/// <summary>The todo rows as the merge sees them, and the hash the events carry.</summary>
internal static class RowMapping
{
    /// <summary>A row as the Sindri side of the merge.</summary>
    /// <param name="row">The row.</param>
    /// <param name="clock">Gives the zone a done day is read in.</param>
    /// <returns>The todo.</returns>
    public static SindriTodo ToSindri(TodoRow row, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(clock);
        var origin = row.Removed ? string.Equals(row.RemovedBy, "obsidian", StringComparison.Ordinal) ? NoteOrigin.Obsidian : NoteOrigin.Sindri : (NoteOrigin?)null;
        DateOnly? doneOn = row.DoneAt is { } at ? DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, clock.LocalTimeZone).DateTime) : null;
        return new SindriTodo(row.Id, row.Text, row.Due, row.Links, row.DoneAt is not null, doneOn, row.Removed, origin, row.LastLineHash, row.Version);
    }

    /// <summary>A stored conflict side as the merge's note side.</summary>
    /// <param name="side">The side.</param>
    /// <returns>The side.</returns>
    public static NoteSide ToSide(TodoSide side)
    {
        ArgumentNullException.ThrowIfNull(side);
        return new NoteSide(side.Text, side.Due, side.Links, side.Done, side.Removed);
    }

    /// <summary>A merge side as the event's side.</summary>
    /// <param name="side">The side.</param>
    /// <returns>The side.</returns>
    public static TodoSide ToEventSide(NoteSide side)
    {
        ArgumentNullException.ThrowIfNull(side);
        return new TodoSide(side.Text, side.Due, side.Links, side.Done, side.Removed);
    }

    /// <summary>The lower-case hex sha256 of a text, as a read of the note reports it.</summary>
    /// <param name="text">The text.</param>
    /// <returns>64 hex characters.</returns>
    public static string HashOf(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    /// <summary>The hash as an event carries it, the form a snapshot of the note reports.</summary>
    /// <param name="hex">The hex hash.</param>
    /// <returns>'sha256:' and the hash.</returns>
    public static string Tag(string hex) => "sha256:" + hex;
}
