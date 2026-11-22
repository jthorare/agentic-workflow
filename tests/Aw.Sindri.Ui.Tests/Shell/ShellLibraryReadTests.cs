using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.ViewModels.Todo;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>Settings' Library card and todo note card both show GET /sindri/library: opening Settings and each event read it once for both (#3801).</summary>
[Lane(TestLane.Unit)]
public sealed class ShellLibraryReadTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly FakeSindriApi _server = new()
    {
        Catalog = RouteAnswer.Api("commands", "catalog"),
        QueueView = RouteAnswer.SpecShaped("queue", "extra-p4-waiting"),
        NeedsYouView = RouteAnswer.Api("needs-you", "empty"),
        TodoNoteAnswer = new RouteAnswer(HttpStatusCode.OK, TodoNoteSetupSample.NoteIn("not set")),   // why: a note that is not set shows the vaults the library read named
    };

    private async Task<ShellViewModel> OnSettings()
    {
        var records = new InMemoryEndpointRecords();
        records.Write(A.Record());
        var shell = new ShellViewModel(records, _server, new FakeTimeProvider(A.Noon), new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await ShellWait.Until(shell, () => shell.Health.ViewReads > 0, Token);   // why: the connect pass's own refresh is over, so Settings opens after it and reads the library once (#3887)

        shell.GoTo(PageId.Settings);
        await ShellWait.Until(shell, () => !shell.LibraryRoots.IsLoading && shell.TodoNoteCard.Stage != TodoNoteCardStage.Reading, Token);
        return shell;
    }

    [Fact]
    public Task Opening_Settings_reads_the_library_once_and_both_cards_show_it() => UiThread.Run(async () =>
    {
        var shell = await OnSettings();

        Assert.Equal((1, false, true), (_server.LibraryCalls, shell.LibraryRoots.IsLoading, shell.LibraryRoots.HasRows));
        Assert.Equal(TodoNoteCardStage.Form, shell.TodoNoteCard.Stage);
    }, Token);

    [Fact]
    public Task An_event_on_Settings_reads_the_library_once_for_both_cards() => UiThread.Run(async () =>
    {
        var shell = await OnSettings();
        var before = _server.LibraryCalls;
        Assert.False(shell.LibraryRoots.Rows[0].IsOff);
        _server.LibraryAnswer = RouteAnswer.Api("library", "empty");

        await shell.Health.RefreshViews(shell.Health.ApiUrl()!, Token);   // why: what an event runs

        // why: the "empty" library golden has no docs and no vault but still lists three roots (aw and two repos), so the card keeps its rows and shows the new answer in them (aw is off there, on in "ok").
        Assert.Equal((before + 1, 3, false), (_server.LibraryCalls, shell.LibraryRoots.Rows.Count, shell.LibraryRoots.IsEmpty));
        Assert.True(shell.LibraryRoots.Rows[0].IsOff, "the second read's answer did not reach the card");
        Assert.Equal(TodoNoteCardStage.NoVaults, shell.TodoNoteCard.Stage);
    }, Token);

    // #3887: the todo note card's own read (GET /sindri/todo-note) is another route; it needs the shared library answer only after the note, so it must not wait for the library to start.
    [Fact]
    public Task An_event_on_Settings_starts_the_note_read_before_the_library_answers() => UiThread.Run(async () =>
    {
        var shell = await OnSettings();
        var notes = _server.TodoNoteCalls;
        var library = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _server.GateNextLibraryRead(library);

        var refresh = shell.Health.RefreshViews(shell.Health.ApiUrl()!, Token);   // why: what an event runs
        var started = _server.TodoNoteCalls > notes;
        library.SetResult();
        await refresh;

        Assert.True(started, "the note read waited for the library read to answer");
        Assert.Equal(TodoNoteCardStage.Form, shell.TodoNoteCard.Stage);
    }, Token);
}
