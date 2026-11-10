using Aw.Kernel.Testing;
using Aw.Sindri.Core.Todos.Note;
using Aw.Sindri.Features.SyncTodoNote;

namespace Aw.Sindri.Tests.Todos.Sync;

/// <summary>The library feed's skip list holds the todo note, in the vault the profile names and nowhere else.</summary>
[Lane(TestLane.Unit)]
public sealed class TodoNoteLibrarySkipsTests
{
    private readonly TodoHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_note_is_skipped_in_its_own_vault()
    {
        _h.NoteLocator.Place = new NotePlace("notes", "Inbox/Todo.md", "/vault");

        Assert.Equal(["Inbox/Todo.md"], await new TodoNoteLibrarySkips(_h.NoteLocator).In("notes", Token), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Another_root_skips_nothing()
    {
        _h.NoteLocator.Place = new NotePlace("notes", "Todo.md", "/vault");

        Assert.Empty(await new TodoNoteLibrarySkips(_h.NoteLocator).In("aw", Token));
    }

    [Fact]
    public async Task No_key_skips_nothing()
    {
        _h.NoteLocator.Place = null;

        Assert.Empty(await new TodoNoteLibrarySkips(_h.NoteLocator).In("notes", Token));
    }
}
