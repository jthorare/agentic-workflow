using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Resources;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Resources;

/// <summary>The Resources page in the shell: G R and the palette open it, it is not a tab, and it reads only while it is the page shown.</summary>
[Lane(TestLane.Unit)]
public sealed class ShellResourcesTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly InMemoryPreferencesStore _store = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), NeedsYouView = RouteAnswer.Api("needs-you", "empty") };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ShellViewModel> Shell()
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, new FakeTimeProvider(A.Noon), new Preferences(_store), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        for (var i = 0; i < 400 && shell.State != ShellState.Normal; i++)
        {
            await Task.Delay(5, Token);
        }

        return shell;
    }

    [Fact]
    public Task G_R_goes_to_the_Resources_page_which_is_not_a_tab_and_then_reads() => UiThread.Run(async () =>
    {
        _server.SeedResources(new ResourceView("workflow:triage", "database", "triage.db", 1024, "/state/triage.db"));
        var shell = await Shell();
        Assert.Empty(_server.ResourceCallLog);   // not shown yet: nothing was read

        _ = shell.PressKey("G");
        _ = shell.PressKey("R");
        await shell.ResourcesPage.CatchUp;

        Assert.Equal((PageId.Resources, true, "Resources"), (shell.CurrentPage, shell.IsResources, shell.PageName));
        Assert.DoesNotContain(shell.Navigation, n => n.Page == PageId.Resources);
        Assert.Contains(PageId.Resources, shell.Pages);
        _ = Assert.Single(shell.ResourcesPage.Rows);
    }, Token);

    [Fact]
    public Task The_palette_lists_Resources_with_its_chord() => UiThread.Run(async () =>
    {
        var shell = await Shell();
        shell.Palette.Open();
        var entries = shell.Palette.Sections.Single(s => string.Equals(s.Heading, Labels.PaletteGoTo, StringComparison.Ordinal)).Entries;

        Assert.Contains(entries, e => string.Equals(e.Label + " " + e.Key, "Resources G R", StringComparison.Ordinal));
    }, Token);
}
