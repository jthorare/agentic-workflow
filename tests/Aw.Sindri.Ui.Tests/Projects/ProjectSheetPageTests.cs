using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Projects;
using static Aw.Sindri.Ui.Tests.Projects.ProjectsFixture;

namespace Aw.Sindri.Ui.Tests.Projects;

/// <summary>The page's side of a card's sheet: the link a card opens, and where focus goes when a write takes a card off the page.</summary>
[Lane(TestLane.Unit)]
public sealed class ProjectSheetPageTests
{
    private const string Kitchen = "prj_0000000000000000000000000R";
    private const string Mobile = "prj_0000000000000000000000000H";
    private const string Checkout = "linear:project/00000000-0000-4000-8000-000000000001";

    private readonly ProjectsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ProjectDoneEventArgs Done(ProjectDone kind, string id, bool finished = false) => new(kind, id, "said", finished);

    // The mixed list without one project, as the page reads it after the write.
    private void ServeWithout(string id)
    {
        var list = Golden("mixed");
        var projects = list["projects"]!.AsArray();
        var gone = projects.Single(p => string.Equals(p!["id"]!.GetValue<string>(), id, StringComparison.Ordinal))!;
        _ = projects.Remove(gone);
        _f.Serve(list);
    }

    private static List<ProjectFocusEventArgs> Watch(ProjectsPageViewModel page)
    {
        var asked = new List<ProjectFocusEventArgs>();
        page.FocusRequested += (_, e) => asked.Add(e);
        return asked;
    }

    [Fact]
    public async Task A_card_links_to_its_sheet_with_the_id_escaped_as_one_segment()
    {
        var page = await _f.Shown(Token);

        var link = page.Groups[0].Cards[0].Link;

        Assert.Equal((DeepLinkKind.Project, "linear%3Aproject%2F00000000-0000-4000-8000-000000000001"), (link.Kind, link.Subject));
        Assert.True(DeepLink.TryParse(link.ToString(), out var parsed));
        Assert.Equal(link, parsed);
    }

    [Fact]
    public async Task Asking_to_open_a_card_raises_the_request_with_its_link()
    {
        var page = await _f.Shown(Token);
        var asked = new List<DeepLink>();
        page.OpenRequested += (_, e) => asked.Add(e.Link);

        page.RequestOpen(page.Groups[1].Cards[0].Link);

        Assert.Equal([page.Groups[1].Cards[0].Link], asked);
    }

    [Fact]
    public async Task After_a_removal_focus_goes_to_the_next_card()
    {
        var page = await _f.Shown(Token);
        var asked = Watch(page);
        ServeWithout(Kitchen);

        await page.Settled(Done(ProjectDone.Removed, Kitchen), Token);

        Assert.Equal((ProjectFocus.Card, Mobile), (Assert.Single(asked).Target, asked[0].CardId));
        Assert.DoesNotContain(page.Groups, g => g.Heading == "Planned");
    }

    [Fact]
    public async Task After_removing_the_last_card_focus_goes_to_the_previous_one()
    {
        var page = await _f.Shown(Token);
        var asked = Watch(page);
        ServeWithout(Mobile);

        await page.Settled(Done(ProjectDone.Removed, Mobile), Token);

        Assert.Equal((ProjectFocus.Card, Kitchen), (Assert.Single(asked).Target, asked[0].CardId));
    }

    [Fact]
    public async Task After_hiding_a_led_card_focus_goes_to_the_next_card()
    {
        var page = await _f.Shown(Token);
        var asked = Watch(page);
        ServeWithout(Checkout);

        await page.Settled(Done(ProjectDone.Hidden, Checkout), Token);

        Assert.Equal((ProjectFocus.Card, Kitchen), (Assert.Single(asked).Target, asked[0].CardId));
    }

    [Fact]
    public async Task After_removing_the_only_card_focus_goes_to_Add_project()
    {
        var list = Golden("mixed");
        var projects = list["projects"]!.AsArray();
        while (projects.Count > 1)
        {
            projects.RemoveAt(1);
        }

        list["finished"] = new JsonArray();
        list["hidden"] = new JsonArray();
        _f.Serve(list);
        var page = await _f.Shown(Token);
        var asked = Watch(page);
        projects.Clear();
        _f.Serve(list);

        await page.Settled(Done(ProjectDone.Removed, Checkout), Token);

        Assert.Equal(ProjectFocus.Add, Assert.Single(asked).Target);
    }

    [Fact]
    public async Task Settling_reads_the_list_again_before_it_asks_for_focus()
    {
        var page = await _f.Shown(Token);
        var reads = _f.Server.ProjectsCalls;
        ServeWithout(Kitchen);

        await page.Settled(Done(ProjectDone.Removed, Kitchen), Token);

        Assert.Equal(reads + 1, _f.Server.ProjectsCalls);
    }

    [Theory]
    [InlineData(false, nameof(ProjectFocus.HiddenFold))]
    [InlineData(true, nameof(ProjectFocus.FinishedFold))]
    public async Task After_an_unhide_focus_goes_to_the_fold_the_card_left_or_Finished_when_it_went_there(bool finished, string fold)
    {
        var page = await _f.Shown(Token);
        var asked = Watch(page);

        await page.Settled(Done(ProjectDone.Unhidden, "linear:project/00000000-0000-4000-8000-000000000002", finished), Token);

        Assert.Equal(Enum.Parse<ProjectFocus>(fold), Assert.Single(asked).Target);
    }
}
