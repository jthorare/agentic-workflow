using Aw.Kernel.Testing;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Todos;
using Aw.Sindri.Core.Todos.Note;
using Aw.Sindri.Features.SyncTodoNote;

namespace Aw.Sindri.Tests.Todos.Sync;

/// <summary>Doctor's 'todo' row (todo spec §6): ok when no key is set or the note syncs, a warning for a note that can't be read, one way, waiting for import, busy, or three failed passes in a row.</summary>
[Lane(TestLane.Unit)]
public sealed class TodoNoteDoctorChecksTests
{
    private readonly TodoHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static TodoNoteStatus Status(TodoNoteState state, int failed = 0, int notAdopted = 0) =>
        new(state, DateTimeOffset.UnixEpoch, failed == 0 ? null : "the note is over 1 MB", failed, notAdopted);

    private async Task<DoctorCheck> Row(bool keySet = true)
    {
        _h.NoteLocator.Place = keySet ? new NotePlace("notes", "Todo.md", "/vault") : null;
        return Assert.Single(await new TodoNoteDoctorChecks(_h.NoteLocator, _h.NoteSync, _h.Slice.Store, _h.NoteFile).Run(Token));
    }

    [Fact]
    public async Task No_key_is_ok_and_says_not_set()
    {
        var row = await Row(keySet: false);

        Assert.Equal(("todo", CheckState.Ok), (row.Name, row.State));
        Assert.Contains("not set", row.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_syncing_note_is_ok_and_names_the_last_sync()
    {
        await _h.Lease();
        await _h.Record(EventTypes.TodoNoteSynced, new TodoNoteSynced("notes", "Todo.md", "h", "b", 1, 1, "service:todo-note"), "todo.note.synced:1");
        _h.NoteSync.Status = Status(TodoNoteState.Syncing);

        var row = await Row();

        Assert.Equal(CheckState.Ok, row.State);
        Assert.Contains("notes/Todo.md", row.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("none yet", row.Detail, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(TodoNoteState.CantRead, "can't read")]
    [InlineData(TodoNoteState.OneWay, "one way")]
    [InlineData(TodoNoteState.WaitingForImport, "waiting for import")]
    [InlineData(TodoNoteState.Busy, "busy")]
    public async Task A_note_in_trouble_warns_in_its_own_words(TodoNoteState state, string words)
    {
        _h.NoteSync.Status = Status(state, failed: state == TodoNoteState.CantRead ? 1 : 0);

        var row = await Row();

        Assert.Equal(CheckState.Warn, row.State);
        Assert.Contains(words, row.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Three_failed_passes_in_a_row_warn_even_when_the_state_says_syncing()
    {
        _h.NoteSync.Status = Status(TodoNoteState.Syncing, failed: 3);

        var row = await Row();

        Assert.Equal(CheckState.Warn, row.State);
        Assert.Contains("failing 3 in a row", row.Detail, StringComparison.Ordinal);
    }

    private static TodoNoteSnapshot Snapshot(string text) => new("sha256:x", NoteParser.Parse(text));

    [Fact]
    public async Task A_note_with_broken_lines_warns_and_names_their_numbers()
    {
        _h.NoteSync.Status = Status(TodoNoteState.Syncing);
        _h.NoteFile.Current = Snapshot("# Todo\n- [ ] ok ^td-01jc5a8xq0n4w7ms2t9r3vbk6e\n- [ ] see [x](http://a.example) ^td-01jc5b2kz8m3r6tq1w9xh4dn7y\n- [ ] two ^td-01jc5c90a2b7k4m8q1r6t3w9xz ^td-01jc5917f3h8n2p5s9v4x1z6bq\n");

        var row = await Row();

        Assert.Equal(CheckState.Warn, row.State);
        Assert.Contains("lines that can't sync: 2 (3, 4)", row.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_clean_note_says_no_line_is_stuck_and_stays_ok()
    {
        _h.NoteSync.Status = Status(TodoNoteState.Syncing);
        _h.NoteFile.Current = Snapshot("# Todo\n- [ ] ok ^td-01jc5a8xq0n4w7ms2t9r3vbk6e\n");

        var row = await Row();

        Assert.Equal(CheckState.Ok, row.State);
        Assert.Contains("lines that can't sync: 0", row.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_note_over_800_KB_warns_it_is_nearly_full()
    {
        _h.NoteSync.Status = Status(TodoNoteState.Syncing);
        _h.NoteFile.Current = Snapshot(string.Concat(Enumerable.Repeat("- [ ] filler line of the note\n", 30_000)));

        var row = await Row();

        Assert.Equal(CheckState.Warn, row.State);
        Assert.Contains("note nearly full", row.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_note_whose_file_is_over_800_KB_warns_even_when_the_rendered_document_is_smaller()
    {
        _h.NoteSync.Status = Status(TodoNoteState.Syncing);
        _h.NoteFile.Current = new TodoNoteSnapshot("sha256:x", NoteParser.Parse("# Todo\n- [ ] ok ^td-01jc5a8xq0n4w7ms2t9r3vbk6e\n"), RawBytes: 900 * 1024);

        var row = await Row();

        Assert.Equal(CheckState.Warn, row.State);
        Assert.Contains("note nearly full", row.Detail, StringComparison.Ordinal);
    }
}
