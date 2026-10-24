using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>The app opens on the page it was closed on, and that page is entered the way GoTo enters it: Context reads the whole map (never a blank stage), Settings reveals its checklist.</summary>
[Lane(TestLane.Unit)]
public sealed class ShellRestoreEntersPageTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_restored_Context_page_reads_the_whole_map_once_connected()
    {
        var store = new InMemoryPreferencesStore();
        await new Preferences(store).Update(_ => UiPreferences.Defaults with { LastPage = nameof(PageId.Context) });
        var records = new InMemoryEndpointRecords();
        records.Write(A.Record());
        var server = new FakeSindriApi { Catalog = RouteAnswer.Api("commands", "catalog"), QueueView = RouteAnswer.SpecShaped("queue", "extra-p4-waiting"), NeedsYouView = RouteAnswer.Api("needs-you", "empty"), WorkflowAnswer = RouteAnswer.Api("workflow-instances", "empty") };
        var shell = new ShellViewModel(records, server, new FakeTimeProvider(A.Noon), new Preferences(store), new FakeClipboard());

        await shell.Restore(Token);
        _ = shell.Run(Token);
        for (var i = 0; i < 400 && !shell.Context.Whole.IsReady; i++)
        {
            await Task.Delay(25, Token);
        }

        Assert.Equal((PageId.Context, true), (shell.CurrentPage, shell.Context.Whole.IsReady));
    }

    [Fact]
    public async Task A_restored_Settings_page_reveals_its_checklist()
    {
        var store = new InMemoryPreferencesStore();
        await new Preferences(store).Update(_ => UiPreferences.Defaults with { LastPage = nameof(PageId.Settings) });
        var shell = new ShellViewModel(new InMemoryEndpointRecords(), new FakeSindriApi(), new FakeTimeProvider(A.Noon), new Preferences(store), new FakeClipboard());
        var revealed = 0;
        shell.Setup.RevealRequested += (_, _) => revealed++;

        await shell.Restore(Token);

        Assert.Equal((PageId.Settings, 1), (shell.CurrentPage, revealed));
    }
}
