using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Connections;

/// <summary>The Connections page and its sheet in the shell, and the Memory page's slot: G O and G M, the palette, the deep links, and the page reading only while it is shown.</summary>
[Lane(TestLane.Unit)]
public sealed class ShellConnectionsTests
{
    private const string Key = "sk-test-5b81c0de7a";

    private readonly InMemoryEndpointRecords _records = new();
    private readonly InMemoryPreferencesStore _store = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), NeedsYouView = RouteAnswer.Api("needs-you", "empty") };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static DeepLink Link(string text) => DeepLink.TryParse(text, out var link) ? link : throw new InvalidOperationException(text);

    private async Task<ShellViewModel> Shell()
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, new FakeTimeProvider(A.Noon), new Preferences(_store), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await Until(() => shell.State == ShellState.Normal);
        return shell;
    }

    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 400 && !condition(); i++)
        {
            await Task.Delay(5, Token);
        }

        Assert.True(condition());
    }

    [Fact]
    public Task G_O_goes_to_the_Connections_tab_which_then_reads() => UiThread.Run(async () =>
    {
        _ = _server.SeedConnection(FakeSindriApi.ConnectionRow("linear:work"));
        var shell = await Shell();

        _ = shell.PressKey("G");
        _ = shell.PressKey("O");
        await Until(() => shell.Connections.Drawn == 1);

        Assert.Equal((PageId.Connections, true, 1), (shell.CurrentPage, shell.IsConnections, shell.Connections.Rows.Count));
        Assert.Equal("Connections", shell.PageName);
        Assert.Equal(PageId.Connections, shell.SelectedNav?.Page);
    }, Token);

    [Fact]
    public Task G_M_goes_to_the_Memory_page_which_is_not_a_tab() => UiThread.Run(async () =>
    {
        var shell = await Shell();

        _ = shell.PressKey("G");
        _ = shell.PressKey("M");

        Assert.Equal((PageId.Memory, true, "Memory"), (shell.CurrentPage, shell.IsMemory, shell.PageName));
        Assert.DoesNotContain(shell.Navigation, n => n.Page == PageId.Memory);
        Assert.Contains(PageId.Memory, shell.Pages);
    }, Token);

    [Fact]
    public Task The_palette_goes_to_both_pages() => UiThread.Run(async () =>
    {
        var shell = await Shell();
        shell.Palette.Open();
        var entries = shell.Palette.Sections.Single(s => string.Equals(s.Heading, Labels.PaletteGoTo, StringComparison.Ordinal)).Entries;

        Assert.Contains(entries, e => string.Equals(e.Label + " " + e.Key, "Connections G O", StringComparison.Ordinal));
        Assert.Contains(entries, e => string.Equals(e.Label + " " + e.Key, "Memory G M", StringComparison.Ordinal));
    }, Token);

    [Fact]
    public Task A_connection_link_opens_the_Connections_page_with_that_connections_sheet() => UiThread.Run(async () =>
    {
        _ = _server.SeedConnection(FakeSindriApi.ConnectionRow("linear:work"));
        var shell = await Shell();

        shell.Navigate(Link("connection/linear:work"));
        await Until(() => shell.ConnectionSheet.Phase == FramePhase.Shown);

        Assert.Equal((PageId.Connections, true, false, "Connection linear:work"), (shell.CurrentPage, shell.IsConnectionSheet, shell.IsSheetPlaceholderShown, shell.SheetName));
        Assert.Equal("Reopen connection linear:work", shell.EdgeTabName);
    }, Token);

    [Fact]
    public Task A_row_asks_the_shell_for_its_sheet() => UiThread.Run(async () =>
    {
        _ = _server.SeedConnection(FakeSindriApi.ConnectionRow("linear:work"));
        var shell = await Shell();
        shell.GoTo(PageId.Connections);
        await Until(() => shell.Connections.Rows.Count == 1);

        shell.Connections.RequestOpen(shell.Connections.Rows[0].Link);

        Assert.Equal((true, "linear:work"), (shell.IsConnectionSheet, shell.SheetSubject));
    }, Token);

    [Fact]
    public Task A_memory_link_opens_the_Memory_page_on_that_store_and_no_sheet() => UiThread.Run(async () =>
    {
        var shell = await Shell();

        shell.Navigate(Link("memory/team-notes"));

        Assert.Equal((PageId.Memory, "team-notes", false), (shell.CurrentPage, shell.RequestedMemoryStore, shell.IsSheetOpen));
    }, Token);

    [Fact]
    public Task The_page_reads_nothing_while_another_page_is_shown() => UiThread.Run(async () =>
    {
        _ = await Shell();
        await Task.Delay(50, Token);

        Assert.DoesNotContain("list", _server.ConnectionCallLog, StringComparer.Ordinal);
    }, Token);

    [Fact]
    public Task A_pasted_key_never_reaches_the_preferences_file() => UiThread.Run(async () =>
    {
        var shell = await Shell();
        shell.GoTo(PageId.Connections);
        var form = shell.Connections.Add;
        form.SelectedPreset = form.Presets.Single(p => string.Equals(p.Id, "rest", StringComparison.Ordinal));
        form.Name = "acme:key";
        form.Key.Text = Key;

        await form.Send(Token);
        shell.GoTo(PageId.Home);   // moving about writes the last page to the preferences

        var stored = System.Text.Encoding.UTF8.GetString(await _store.Read(Token) ?? []);
        Assert.DoesNotContain(Key, stored, StringComparison.Ordinal);
        Assert.Equal(Key, _server.LastAddConnection?.Key);
    }, Token);
}
