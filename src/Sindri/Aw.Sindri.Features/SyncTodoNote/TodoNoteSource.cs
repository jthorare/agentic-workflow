using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Todos;
using Aw.Sindri.Core.Todos.Note;

namespace Aw.Sindri.Features.SyncTodoNote;

/// <summary>Reads the todo note the live profile names, for a pick that re-reads it before acting (todo spec §4.4). With no key set, or no note file yet, there is nothing to read.</summary>
/// <param name="locator">Finds the note.</param>
/// <param name="files">Reads it.</param>
internal sealed class TodoNoteSource(INoteLocator locator, ITodoNoteFiles files) : ITodoNoteSource
{
    public async ValueTask<Result<TodoNoteSnapshot?>> Read(CancellationToken cancellationToken)
    {
        var located = await locator.Locate(cancellationToken).ConfigureAwait(false);
        if (located.IsError)
        {
            return located.Error;
        }

        if (located.Value is not { } place)
        {
            return Results.Success<TodoNoteSnapshot?>(value: null);
        }

        var read = files.Read(place.Path);
        if (read.Problem is not null)
        {
            return SindriErrors.TodoNoteKey.With($"The todo note can't be read: {read.Problem}");
        }

        return read.Text is null || read.Hash is null
            ? Results.Success<TodoNoteSnapshot?>(value: null)
            : Results.Success<TodoNoteSnapshot?>(new TodoNoteSnapshot(read.Hash, NoteParser.Parse(read.Text), read.Bytes));
    }
}
