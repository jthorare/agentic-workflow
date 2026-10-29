using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Projects;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Projects;

/// <summary>A card's sheet in the shell: how it opens, what closes it, and the sentence and focus request after a write that takes its card off the page.</summary>
[Lane(TestLane.Unit)]
public sealed class ProjectSheetShellTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), NeedsYouView = RouteAnswer.Api("needs-you", "empty") };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ShellViewModel> Shell()
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, new FakeTimeProvider(A.Noon), new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
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

    private static DeepLink Link(string id) => new(DeepLinkKind.Project, Uri.EscapeDataString(id));

    private const string Led = "linear:project/00000000-0000-4000-8000-000000000001";

    private void ServeLed() => _server.ProjectAnswers[Led] = new RouteAnswer(HttpStatusCode.OK,
        GoldenFiles.Api("projects-id", "attached").Replace("prj_0000000000000000000000000H", Led, StringComparison.Ordinal).Replace("\"origin\": \"attached\"", "\"origin\": \"led\"", StringComparison.Ordinal).Replace("\"name\": \"Mobile app\"", "\"name\": \"Checkout rewrite\"", StringComparison.Ordinal));

    [Fact]
    public Task A_project_link_goes_to_Projects_and_opens_its_sheet_on_the_card() => UiThread.Run(async () =>
    {
        var shell = await Shell();

        shell.Navigate(Link("prj_0000000000000000000000000H"));
        await Until(() => shell.ProjectSheet.IsShown);

        Assert.Equal((PageId.Projects, true, true, false), (shell.CurrentPage, shell.IsSheetOpen, shell.IsProjectSheet, shell.IsSheetPlaceholderShown));
        Assert.Equal(("Mobile app", "Project Mobile app"), (shell.SheetTitle, shell.SheetName));
    }, Token);

    [Fact]
    public Task A_cards_open_request_opens_the_sheet_and_remembers_the_card_for_Esc() => UiThread.Run(async () =>
    {
        var shell = await Shell();
        shell.GoTo(PageId.Projects);
        await Until(() => !shell.Projects.IsLoading);

        shell.Projects.RequestOpen(shell.Projects.Groups[2].Cards[0].Link);

        Assert.Equal((true, "prj_0000000000000000000000000H"), (shell.IsProjectSheet, shell.OpenerProject));
    }, Token);

    [Fact]
    public Task A_sheet_opened_some_other_way_has_no_opener() => UiThread.Run(async () =>
    {
        var shell = await Shell();
        shell.Projects.RequestOpen(Link("prj_0000000000000000000000000H"));
        shell.Navigate(Link("prj_0000000000000000000000000H"));

        Assert.Null(shell.OpenerProject);
    }, Token);

    [Fact]
    public Task Esc_closes_the_sheet_and_the_edge_tab_reopens_the_card_by_name() => UiThread.Run(async () =>
    {
        var shell = await Shell();
        shell.Navigate(Link("prj_0000000000000000000000000H"));
        await Until(() => shell.ProjectSheet.IsShown);

        shell.CloseSheet();

        Assert.Equal((false, true, "Reopen project Mobile app"), (shell.IsSheetOpen, shell.HasEdgeTab, shell.EdgeTabName));
        Assert.Equal("Mobile app", shell.EdgeTabText);
    }, Token);

    [Fact]
    public Task The_open_sheet_reads_again_on_every_event() => UiThread.Run(async () =>
    {
        var shell = await Shell();
        shell.Navigate(Link("prj_0000000000000000000000000H"));
        await Until(() => shell.ProjectSheet.IsShown);
        var reads = _server.ProjectsRead.Count;

        await shell.Health.Refresh(Token);

        await Until(() => _server.ProjectsRead.Count > reads);
    }, Token);

    [Fact]
    public Task Hiding_closes_the_sheet_announces_it_reloads_the_page_and_moves_focus() => UiThread.Run(async () =>
    {
        ServeLed();
        var shell = await Shell();
        shell.GoTo(PageId.Projects);
        await Until(() => !shell.Projects.IsLoading);
        var asked = new List<ProjectFocusEventArgs>();
        shell.Projects.FocusRequested += (_, e) => asked.Add(e);
        shell.Projects.RequestOpen(shell.Projects.Groups[0].Cards[0].Link);
        await Until(() => shell.ProjectSheet.IsShown);
        var reads = _server.ProjectsCalls;

        await shell.ProjectSheet.Hide(Token);

        Assert.Equal((false, false, "Checkout rewrite hidden"), (shell.IsSheetOpen, shell.HasEdgeTab, shell.NeedsYouAnnouncement));
        Assert.True(_server.ProjectsCalls > reads);
        Assert.Equal(ProjectFocus.Card, Assert.Single(asked).Target);
    }, Token);

    [Fact]
    public Task Removing_closes_the_sheet_and_says_so() => UiThread.Run(async () =>
    {
        var shell = await Shell();
        shell.Navigate(Link("prj_0000000000000000000000000H"));
        await Until(() => shell.ProjectSheet.IsShown);
        shell.ProjectSheet.AskRemove();

        await shell.ProjectSheet.Remove(Token);

        Assert.Equal((false, "Mobile app removed"), (shell.IsSheetOpen, shell.NeedsYouAnnouncement));
    }, Token);

    [Fact]
    public Task Saving_announces_and_leaves_the_sheet_open() => UiThread.Run(async () =>
    {
        var shell = await Shell();
        shell.Navigate(Link("prj_0000000000000000000000000H"));
        await Until(() => shell.ProjectSheet.IsShown);
        shell.ProjectSheet.BeginEdit();

        await shell.ProjectSheet.Save(Token);

        Assert.Equal((true, "Mobile app saved"), (shell.IsSheetOpen, shell.NeedsYouAnnouncement));
    }, Token);

    [Fact]
    public void A_project_links_text_round_trips_and_a_bad_one_does_not_parse()
    {
        var link = Link(Led);

        Assert.True(DeepLink.TryParse(link.ToString(), out var parsed));
        Assert.Equal(("project/linear%3Aproject%2F00000000-0000-4000-8000-000000000001", link), (link.ToString(), parsed));
        Assert.True(DeepLink.TryParse("project/prj_0000000000000000000000000H", out _));
        Assert.False(DeepLink.TryParse("project/", out _));
        Assert.False(DeepLink.TryParse("project/a%2Fb/c", out _));
        Assert.False(DeepLink.TryParse("project/a b", out _));
        Assert.False(DeepLink.TryParse("project/%2E%2E", out _));
        Assert.False(DeepLink.TryParse("project/" + new string('a', 129), out _));
    }
}
