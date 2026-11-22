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
        for (var i = 0; i < 400 && shell.Health.ApiUrl() is null; i++)
        {
            await Task.Delay(25, Token);
        }

        shell.GoTo(PageId.Settings);
        for (var i = 0; i < 400 && (shell.LibraryRoots.IsLoading || shell.TodoNoteCard.Stage == TodoNoteCardStage.Reading); i++)
        {
            await Task.Delay(25, Token);
        }

        return shell;
    }

    [Fact]
    public async Task Opening_Settings_reads_the_library_once_and_both_cards_show_it()
    {
        var shell = await OnSettings();

        Assert.Equal((1, false, true), (_server.LibraryCalls, shell.LibraryRoots.IsLoading, shell.LibraryRoots.HasRows));
        Assert.Equal(TodoNoteCardStage.Form, shell.TodoNoteCard.Stage);
    }

    [Fact]
    public async Task An_event_on_Settings_reads_the_library_once_for_both_cards()
    {
        var shell = await OnSettings();
        var before = _server.LibraryCalls;
        _server.LibraryAnswer = RouteAnswer.Api("library", "empty");

        await shell.Health.RefreshViews(shell.Health.ApiUrl()!, Token);   // why: what an event runs

        Assert.Equal((before + 1, false, true), (_server.LibraryCalls, shell.LibraryRoots.HasRows, shell.LibraryRoots.IsEmpty));
        Assert.Equal(TodoNoteCardStage.NoVaults, shell.TodoNoteCard.Stage);
    }
}
