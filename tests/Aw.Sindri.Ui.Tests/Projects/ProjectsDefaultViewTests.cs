using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Projects;
using static Aw.Sindri.Ui.Tests.Projects.ProjectsFixture;

namespace Aw.Sindri.Ui.Tests.Projects;

/// <summary>The view the Projects page opens on (issue 4124): Needs me only when at least one project waits on the owner, else the full list.</summary>
[Lane(TestLane.Unit)]
public sealed class ProjectsDefaultViewTests
{
    private readonly ProjectsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static JsonObject Truth(bool blockedOnYou) => new()
    {
        ["openPrs"] = new JsonArray(),
        ["openItems"] = 0,
        ["doneItems"] = 0,
        ["totalItems"] = 0,
        ["runningAgents"] = 0,
        ["health"] = null,
        ["blockedOnYou"] = blockedOnYou,
    };

    // The mixed golden with the flag served on its three open projects; the finished and hidden cards carry no truth.
    private void ServeFlags(bool checkout, bool kitchen, bool mobile)
    {
        var list = Golden("mixed");
        var projects = list["projects"]!.AsArray();
        (projects[0]!["truth"], projects[1]!["truth"], projects[2]!["truth"]) = (Truth(checkout), Truth(kitchen), Truth(mobile));
        foreach (var card in list["finished"]!.AsArray().Concat(list["hidden"]!.AsArray()))
        {
            card!["truth"] = null;
        }

        _f.Serve(list);
    }

    [Fact]
    public async Task The_page_opens_on_the_full_list_when_no_project_waits_on_you()
    {
        ServeFlags(checkout: false, kitchen: false, mobile: false);

        var page = await _f.Shown(Token, defaultView: true);

        Assert.Equal((ProjectView.All, true), (page.View, page.Tabs[2].IsOn));
    }

    [Fact]
    public async Task The_page_opens_on_Needs_me_when_one_project_waits_on_you()
    {
        ServeFlags(checkout: false, kitchen: true, mobile: false);

        var page = await _f.Shown(Token, defaultView: true);

        Assert.Equal((ProjectView.NeedsMe, true), (page.View, page.Tabs[0].IsOn));
    }

    [Fact]
    public async Task The_Needs_me_tab_stays_in_the_bar_with_a_count_of_zero()
    {
        ServeFlags(checkout: false, kitchen: false, mobile: false);

        var page = await _f.Shown(Token, defaultView: true);

        Assert.Equal((ProjectView.NeedsMe, 0), (page.Tabs[0].View, page.Tabs[0].Count));
    }
}
