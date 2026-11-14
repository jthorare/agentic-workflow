using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Todos;
using Aw.Sindri.Features.GetTodoNote;

namespace Aw.Sindri.Tests.Todos;

/// <summary>GET /sindri/todos/note on a home with no profile is "not set up", not a failed read (#3113): no error and no doctor line. A vault that is set and can't be read stays "can't read".</summary>
[Lane(TestLane.Unit)]
public sealed class TodoNoteNotSetUpTests
{
    private readonly TodoHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<GetTodoNoteResponse> Read()
    {
        await _h.Lease();
        using var cli = _h.Cli();
        var note = await _h.Note().Handle(new GetTodoNoteRequest(), Token);
        Assert.True(note.IsSuccess, note.IsError ? note.Error.ToString() : null);
        return note.Value;
    }

    [Fact]
    public void The_not_set_up_state_has_the_words_not_set_up() =>
        Assert.Equal("not set up", TodoNoteWords.Of(TodoNoteState.NotSetUp));

    [Fact]
    public async Task A_home_with_no_profile_reads_not_set_up_with_no_error_and_no_vault()
    {
        _h.NoteLocator.Fails = SindriErrors.ProfileMissing.With("No profile found");

        var note = await Read();

        Assert.Equal(("not set up", null, null, null), (note.State, note.LastError, note.Vault, note.Note));
    }

    [Fact]
    public async Task A_profile_with_no_vault_key_reads_not_set_with_no_error()
    {
        var note = await Read();

        Assert.Equal(("not set", null), (note.State, note.LastError));
    }

    [Fact]
    public async Task A_profile_file_on_disk_that_cant_be_read_stays_cant_read_not_set_up()
    {
        _h.NoteLocator.Fails = SindriErrors.ProfileMissing.With("/home/thor/profile.yaml can't be read: Access to the path is denied.");
        _h.ProfileFile = new FileOnDisk();

        var note = await Read();

        Assert.Equal(("can't read", true), (note.State, note.LastError is not null));
    }

    [Fact]
    public async Task A_vault_that_is_set_and_cannot_be_resolved_stays_cant_read_with_its_error()
    {
        _h.NoteLocator.Fails = SindriErrors.TodoNoteKey.With("todo.obsidian.vault: 'notes' can't be resolved to a folder");

        var note = await Read();

        Assert.Equal(("can't read", true), (note.State, note.LastError is not null));
    }

    private sealed class FileOnDisk : IProfileFile
    {
        public bool Exists() => true;
    }
}
