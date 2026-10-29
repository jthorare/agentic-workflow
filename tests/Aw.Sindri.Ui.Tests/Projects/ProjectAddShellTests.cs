using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Projects;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Projects;

/// <summary>The Add project sheet in the shell (projects page spec §3.2): how it opens, what closes it, and what a finished Save says and focuses.</summary>
[Lane(TestLane.Unit)]
public sealed class ProjectAddShellTests
{
    private const string Kitchen = "prj_0000000000000000000000000R";

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

    private async Task<ShellViewModel> OnProjects()
    {
        var shell = await Shell();
        shell.GoTo(PageId.Projects);
        await Until(() => !shell.Projects.IsLoading);
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

    private void Accept(string project, string name) => _server.ProjectWriteAnswer = new RouteAnswer(HttpStatusCode.OK,
        $$"""{"accepted":{"commandId":"cmd_fake","sequence":41},"project":"{{project}}","version":1,"name":"{{name}}"}""");

    [Fact]
    public Task Add_project_opens_its_sheet_over_the_page_with_no_edge_tab() => UiThread.Run(async () =>
    {
        var shell = await OnProjects();

        shell.Projects.RequestAdd();

        Assert.Equal((PageId.Projects, true, true, false, false), (shell.CurrentPage, shell.IsSheetOpen, shell.IsProjectAddSheet, shell.IsProjectSheet, shell.IsSheetPlaceholderShown));
        Assert.Equal(("Add project", "Add project", false), (shell.SheetTitle, shell.SheetName, shell.HasEdgeTab));
    }, Token);

    [Fact]
    public Task The_sheets_link_goes_to_Projects_from_any_page() => UiThread.Run(async () =>
    {
        var shell = await Shell();

        shell.Navigate(DeepLink.ProjectAdd);

        Assert.Equal((PageId.Projects, true, true), (shell.CurrentPage, shell.IsSheetOpen, shell.IsProjectAddSheet));
    }, Token);

    [Fact]
    public Task Esc_closes_the_sheet_and_leaves_no_edge_tab() => UiThread.Run(async () =>
    {
        var shell = await OnProjects();
        shell.Projects.RequestAdd();

        shell.CloseSheet();

        Assert.Equal((false, false, false), (shell.IsSheetOpen, shell.HasEdgeTab, shell.IsEdgeTabShown));
    }, Token);

    [Fact]
    public Task Opening_the_sheet_again_starts_the_form_over() => UiThread.Run(async () =>
    {
        var shell = await OnProjects();
        shell.Projects.RequestAdd();
        shell.ProjectAdd.Name = "Half typed";
        shell.CloseSheet();

        shell.Projects.RequestAdd();

        Assert.Equal((string.Empty, true), (shell.ProjectAdd.Name, shell.ProjectAdd.IsFromLinear));
    }, Token);

    [Fact]
    public Task A_finished_Save_closes_the_sheet_says_it_reads_the_page_again_and_asks_for_the_new_card() => UiThread.Run(async () =>
    {
        var shell = await OnProjects();
        var asked = new List<ProjectFocusEventArgs>();
        shell.Projects.FocusRequested += (_, e) => asked.Add(e);
        shell.Projects.RequestAdd();
        shell.ProjectAdd.Name = "Kitchen";
        Accept(Kitchen, "Kitchen");
        var reads = _server.ProjectsCalls;

        await shell.ProjectAdd.Save(Token);
        await Until(() => asked.Count == 1);

        Assert.Equal((false, false, "Kitchen added"), (shell.IsSheetOpen, shell.HasEdgeTab, shell.NeedsYouAnnouncement));
        Assert.True(_server.ProjectsCalls > reads);
        Assert.Equal((ProjectFocus.Card, Kitchen), (asked[0].Target, asked[0].CardId));
    }, Token);

    [Fact]
    public Task A_new_project_the_page_does_not_show_sends_focus_to_Add_project() => UiThread.Run(async () =>
    {
        var shell = await OnProjects();
        var asked = new List<ProjectFocusEventArgs>();
        shell.Projects.FocusRequested += (_, e) => asked.Add(e);
        shell.Projects.RequestAdd();
        shell.ProjectAdd.Name = "Elsewhere";
        Accept("prj_000000000000000000000000ZZ", "Elsewhere");

        await shell.ProjectAdd.Save(Token);
        await Until(() => asked.Count == 1);

        Assert.Equal(ProjectFocus.Add, asked[0].Target);
    }, Token);

    [Fact]
    public Task A_refused_Save_keeps_the_sheet_open_with_its_error() => UiThread.Run(async () =>
    {
        var shell = await OnProjects();
        shell.Projects.RequestAdd();
        shell.ProjectAdd.Name = "Kitchen";
        _server.ProjectWriteAnswer = RouteAnswer.Problem(HttpStatusCode.BadRequest, "SND-PROJECT-004", "name: too long");

        await shell.ProjectAdd.Save(Token);

        Assert.Equal((true, true, "Kitchen"), (shell.IsSheetOpen, shell.ProjectAdd.SaveError.Length > 0, shell.ProjectAdd.Name));
    }, Token);
}
