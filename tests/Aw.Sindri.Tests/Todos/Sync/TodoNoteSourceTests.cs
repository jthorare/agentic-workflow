using Aw.Kernel.Testing;
using Aw.Sindri.Core.Todos.Note;
using Aw.Sindri.Features.SyncTodoNote;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Todos.Sync;

/// <summary>The real note source (todo spec §4.4: a pick re-reads the note): the profile's note parsed with its hash, nothing when no key is set or the file is missing, an error when it can't be read.</summary>
[Lane(TestLane.Unit)]
public sealed class TodoNoteSourceTests
{
    private readonly FakeTodoNoteFiles _files = new();
    private readonly NoteLocatorStub _locator = new() { Place = new NotePlace("notes", "Todo.md", "/vault") };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_note_is_read_and_parsed_with_its_hash()
    {
        _files.Files["/vault/Todo.md"] = "# Todo\n\n- [ ] call the dentist\n";

        var read = await new TodoNoteSource(_locator, _files).Read(Token);

        Assert.True(read.IsSuccess);
        Assert.Equal(_files.Read("/vault/Todo.md").Hash, read.Value!.Hash);
        Assert.NotEmpty(read.Value.Document.Lines);
    }

    [Fact]
    public async Task No_key_and_no_file_both_read_as_no_note()
    {
        _files.Files.Clear();
        Assert.Null((await new TodoNoteSource(_locator, _files).Read(Token)).Value);

        _locator.Place = null;
        Assert.Null((await new TodoNoteSource(_locator, _files).Read(Token)).Value);
    }

    [Fact]
    public async Task A_note_that_cannot_be_read_is_an_error_naming_why()
    {
        _files.Files["/vault/Todo.md"] = "x";
        _files.Problems["/vault/Todo.md"] = "the note is a symlink";

        var read = await new TodoNoteSource(_locator, _files).Read(Token);

        Assert.True(read.IsError);
        Assert.Contains("symlink", read.Error.Message, StringComparison.Ordinal);
    }
}
