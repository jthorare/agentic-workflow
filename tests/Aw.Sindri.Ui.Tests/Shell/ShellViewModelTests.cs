using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>
/// The shell's contract (UI spec §3.2, §3.3, §4.1; U0 plan Unit C item 6): the shell over the in-memory endpoint record,
/// API, clock and preferences, restored from the preferences the way the app starts it.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class ShellViewModelTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly InMemoryPreferencesStore _preferences = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // The shell as the app starts it: built over the ports, then restored from the stored preferences.
    private async Task<ShellViewModel> Shell()
    {
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(_preferences), new FakeClipboard());
        await shell.Restore(Token);
        return shell;
    }

    // Runs the shell until it reaches the state.
    private async Task<ShellViewModel> Running(ShellState until)
    {
        var shell = await Shell();
        _ = shell.Run(Token);
        await Until(() => shell.State == until);

        Assert.Equal(until, shell.State);
        return shell;
    }

    // Stream events reach the shell on the thread pool: wait for them with a bound, not a count of yields.
    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 400 && !condition(); i++)
        {
            await Task.Delay(5, Token);
        }
    }

    // Screens spec §3: seven pages, each with its G-chord key; Settings is not a tab.
    [Fact]
    public async Task Nav_is_Home_Workflows_Work_Recipes_Context_Agents_System()
    {
        var shell = await Shell();

        Assert.Equal(
            [PageId.Home, PageId.Workflows, PageId.Work, PageId.Recipes, PageId.Context, PageId.Agents, PageId.System],
            shell.Navigation.Select(n => n.Page));
        Assert.Equal(
            [Labels.Home, Labels.Workflows, Labels.Work, Labels.Recipes, Labels.Context, Labels.Agents, Labels.System],
            shell.Navigation.Select(n => n.Label),
            StringComparer.Ordinal);
        Assert.Equal(["H", "W", "Q", "R", "C", "A", "."], shell.Navigation.Select(n => n.Key), StringComparer.Ordinal);
        Assert.All(shell.Navigation, n => Assert.Equal(string.Empty, n.Count));
    }

    // Screens spec §4.8: a page whose phase hasn't landed says which phase brings it and the API it waits for, never faked content.
    [Theory]
    [InlineData(PageId.Work, "arrives with U3' and the workflow routes (API P11)")]
    [InlineData(PageId.Context, "arrives with U9 and the context lens routes (CTX phase 4)")]
    internal async Task Unbuilt_pages_show_arrives_with_their_phase(PageId page, string words)
    {
        await Task.CompletedTask;

        Assert.Equal(words, Labels.ArrivesWith(page));
        Assert.Null(Labels.ArrivesWith(PageId.Home));
        Assert.Null(Labels.ArrivesWith(PageId.Workflows));   // built in U8
        Assert.Null(Labels.ArrivesWith(PageId.Recipes));
        Assert.Null(Labels.ArrivesWith(PageId.Agents));   // built in U11a
        Assert.Null(Labels.ArrivesWith(PageId.System));
    }

    // Screens spec §3: G then a page's key; G S is System as G . is.
    [Theory]
    [InlineData("H", PageId.Home)]
    [InlineData("W", PageId.Workflows)]
    [InlineData("Q", PageId.Work)]
    [InlineData("R", PageId.Recipes)]
    [InlineData("C", PageId.Context)]
    [InlineData("A", PageId.Agents)]
    [InlineData(".", PageId.System)]
    [InlineData("S", PageId.System)]
    [InlineData("N", PageId.Home)]
    [InlineData("D", PageId.Home)]
    internal async Task Chords_follow_screens_section_3(string second, PageId page)
    {
        var shell = await Shell();
        shell.GoTo(page == PageId.Context ? PageId.Agents : PageId.Context);

        Assert.True(shell.PressKey("G"));
        Assert.True(shell.IsChordPending);
        Assert.Equal("G …", shell.ChordHint);
        Assert.True(shell.PressKey(second));

        Assert.Equal(page, shell.CurrentPage);
        Assert.False(shell.IsChordPending);
        Assert.Equal(string.Empty, shell.ChordHint);
    }

    [Fact]
    public async Task G_D_filters_Home_to_the_decision_rows_and_G_H_clears_it()
    {
        var shell = await Shell();
        shell.GoTo(PageId.Work);

        _ = shell.PressKey("G");
        _ = shell.PressKey("D");
        Assert.Equal(PageId.Home, shell.CurrentPage);
        Assert.Equal(HomeFilter.Decisions, shell.HomeFilter);

        _ = shell.PressKey("G");
        _ = shell.PressKey("N");
        Assert.Equal(HomeFilter.All, shell.HomeFilter);
    }

    [Fact]
    public async Task A_chord_waits_one_second_and_any_other_key_cancels_it()
    {
        var shell = await Shell();

        _ = shell.PressKey("G");
        _clock.Advance(TimeSpan.FromMilliseconds(1001));
        Assert.False(shell.IsChordPending);
        Assert.False(shell.PressKey("W"));
        Assert.Equal(PageId.Home, shell.CurrentPage);

        _ = shell.PressKey("G");
        Assert.False(shell.PressKey("X"));
        Assert.False(shell.IsChordPending);
        Assert.False(shell.PressKey("W"));
        Assert.Equal(PageId.Home, shell.CurrentPage);
    }

    [Fact]
    public async Task A_second_key_alone_is_not_a_chord()
    {
        var shell = await Shell();

        Assert.False(shell.PressKey("W"));
        Assert.Equal(PageId.Home, shell.CurrentPage);
    }

    [Theory]
    [InlineData("workflow/INC-14", DeepLinkKind.Workflow, PageId.Workflows)]
    [InlineData("workflow/ITM-01JC8/step", DeepLinkKind.Workflow, PageId.Workflows)]
    [InlineData("recipe/workflow.project-build", DeepLinkKind.Recipe, PageId.Recipes)]
    [InlineData("node/feature:tabs.tab-manager", DeepLinkKind.Node, PageId.Context)]
    [InlineData("session/codex/snd-01JE7.2", DeepLinkKind.Session, PageId.Agents)]
    [InlineData("system/temporal", DeepLinkKind.System, PageId.System)]
    internal async Task A_rev8_link_opens_its_page_and_the_sheet(string text, DeepLinkKind kind, PageId page)
    {
        var shell = await Shell();
        Assert.True(DeepLink.TryParse(text, out var link));
        Assert.Equal(kind, link.Kind);

        shell.Navigate(link);

        Assert.Equal(page, shell.CurrentPage);
        Assert.True(shell.IsSheetOpen);
        Assert.Equal(link.Subject, shell.SheetSubject);
    }

    [Fact]
    public async Task Every_page_is_reachable_and_Home_is_the_default()
    {
        var shell = await Shell();

        Assert.Equal(Enum.GetValues<PageId>(), shell.Pages);
        Assert.Equal(PageId.Home, shell.CurrentPage);
    }

    [Fact]
    public async Task Going_to_a_page_shows_it_and_says_so()
    {
        var shell = await Shell();
        var changed = new List<string?>();
        shell.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        shell.GoTo(PageId.System);

        Assert.Equal(PageId.System, shell.CurrentPage);
        Assert.Contains(nameof(ShellViewModel.CurrentPage), changed, StringComparer.Ordinal);
    }

    [Fact]
    public async Task An_item_link_opens_the_sheet_and_Esc_leaves_its_edge_tab()
    {
        var shell = await Shell();
        Assert.True(DeepLink.TryParse("item/ITM-01JC8", out var link));

        shell.Navigate(link);
        Assert.True(shell.IsSheetOpen);
        Assert.Equal("ITM-01JC8", shell.SheetSubject);

        shell.CloseSheet();
        Assert.False(shell.IsSheetOpen);
        Assert.True(shell.HasEdgeTab);
        Assert.Equal("ITM-01JC8", shell.SheetSubject);
    }

    [Fact]
    public async Task The_home_needs_you_link_goes_Home_and_leaves_the_sheet_alone()
    {
        var shell = await Shell();
        shell.GoTo(PageId.Work);

        shell.Navigate(DeepLink.HomeNeedsYou);

        Assert.Equal(PageId.Home, shell.CurrentPage);
        Assert.False(shell.IsSheetOpen);
        Assert.False(shell.HasEdgeTab);
    }

    [Fact]
    public async Task Before_the_first_answer_the_shell_is_connecting()
    {
        var shell = await Shell();

        Assert.Equal((ShellState.Connecting, Labels.Connecting), (shell.State, shell.Pill.Text));
    }

    [Fact]
    public async Task No_endpoint_record_is_not_running_with_the_reason()
    {
        var shell = await Running(ShellState.NotRunning);

        // I7: the reason's words lead; the record's path is its own value, shortened.
        await Until(() => shell.Pill.Text == Labels.NotRunning);
        Assert.Equal(Labels.NoRecord, shell.Reason);
        Assert.Equal(StatusText.ShortPath(_records.Location), shell.ReasonDetail);
        Assert.Equal(Labels.NotRunning, shell.Pill.Text);
    }

    [Fact]
    public async Task A_status_without_an_api_version_is_normal()
    {
        _records.Write(A.Record());

        var shell = await Running(ShellState.Normal);

        await Until(() => shell.Pill.Text.StartsWith(Labels.Running, StringComparison.Ordinal));
        Assert.StartsWith(Labels.Running, shell.Pill.Text, StringComparison.Ordinal);
    }

    // Main's #231: while views.db migrates, /status answers views: null ("no views yet"): the app starts cleanly, says Running, and
    // shows no views banner (nothing is stalled or rebuilding that it knows of).
    [Fact]
    public async Task A_status_with_no_views_yet_starts_cleanly_and_says_running()
    {
        _records.Write(A.Record());
        _server.Current = A.Status() with { Views = null, Health = null };

        var shell = await Running(ShellState.Normal);

        await Until(() => shell.Pill.Text.StartsWith(Labels.Running, StringComparison.Ordinal));
        Assert.Equal((ShellState.Normal, Labels.Running, false), (shell.State, shell.Pill.Word, shell.Health.Banner.IsShown));
    }

    [Fact]
    public async Task A_status_of_another_api_version_is_a_version_mismatch()
    {
        _records.Write(A.Record());
        _server.Current = A.Status() with { ApiVersion = 2 };

        var shell = await Running(ShellState.VersionMismatch);

        Assert.Equal(Labels.Versions(2, SindriApiClient.AppApiVersion), shell.Reason);
    }

    [Fact]
    public async Task An_answer_this_build_cant_read_is_unreadable_and_names_its_code()
    {
        _records.Write(A.Record());
        _server.UnreadableStatus = true;

        var shell = await Running(ShellState.Unreadable);

        Assert.Contains("SND-UI-003", shell.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Thirty_seconds_without_an_event_or_ping_is_stale_and_an_event_freshens_it()
    {
        _records.Write(A.Record());
        var shell = await Running(ShellState.Normal);
        _server.FailPing = true;

        _clock.Advance(ShellViewModel.StaleAfter);
        await Until(() => shell.State == ShellState.Stale);

        await Until(() => shell.Pill.Text.Contains(Labels.Stale, StringComparison.Ordinal));
        Assert.Equal(ShellState.Stale, shell.State);
        Assert.Contains(Labels.Stale, shell.Pill.Text, StringComparison.Ordinal);

        _server.Push(A.Event(1));
        await Until(() => shell.State == ShellState.Normal);

        Assert.Equal(ShellState.Normal, shell.State);
    }

    // §11.2: the page region is read with the page it shows, not always "Home".
    [Theory]
    [InlineData(PageId.Home, Labels.Home)]
    [InlineData(PageId.Workflows, Labels.Workflows)]
    [InlineData(PageId.Work, Labels.Work)]
    [InlineData(PageId.Recipes, Labels.Recipes)]
    [InlineData(PageId.Context, Labels.Context)]
    [InlineData(PageId.Agents, Labels.Agents)]
    [InlineData(PageId.System, Labels.System)]
    [InlineData(PageId.Settings, Labels.Settings)]
    internal async Task The_page_region_is_named_after_the_page_shown(PageId page, string name)
    {
        var shell = await Shell();
        var changed = new List<string?>();
        shell.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        shell.GoTo(page);

        Assert.Equal(name, shell.PageName);
        Assert.True(page == PageId.Home || changed.Contains(nameof(ShellViewModel.PageName)), "PageName change raised");
    }

    [Fact]
    public async Task The_last_page_is_remembered_and_reopened()
    {
        (await Shell()).GoTo(PageId.Context);

        Assert.Equal("Context", (await new Preferences(_preferences).Load(Token)).LastPage);
        Assert.Equal(PageId.Context, (await Shell()).CurrentPage);
    }
}
