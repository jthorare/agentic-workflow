using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Projects;
using static Aw.Sindri.Ui.Tests.Projects.ProjectsFixture;

namespace Aw.Sindri.Ui.Tests.Projects;

/// <summary>A group keeps its folded state when the grouping pick changes but the groups drawn stay the same (issue 4124): with Sindri's facts missing, Health and Repo draw the state groups.</summary>
[Lane(TestLane.Unit)]
public sealed class ProjectsGroupFoldKeyTests
{
    private readonly ProjectsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private void ServeFactsMissing()
    {
        var list = Golden("mixed");
        list["freshness"]!.AsArray().Add(new JsonObject { ["source"] = "sindri", ["state"] = "failing", ["text"] = "SND-REVIEWS-012: reviews.db is locked", ["at"] = null });
        _f.Serve(list);
    }

    [Fact]
    public async Task A_state_group_folded_under_State_stays_folded_when_Health_is_picked_with_the_facts_missing()
    {
        ServeFactsMissing();
        var page = await _f.Shown(Token, ProjectGrouping.State);
        var heading = page.Groups[0].Heading;
        page.ToggleGroup(page.Groups[0]);

        page.SetGrouping(ProjectGrouping.Health);

        Assert.Equal((heading, true), (page.Groups[0].Heading, page.Groups[0].IsCollapsed));
    }

    [Fact]
    public async Task A_state_group_folded_under_Health_stays_folded_when_State_is_picked()
    {
        ServeFactsMissing();
        var page = await _f.Shown(Token, ProjectGrouping.Health);
        var heading = page.Groups[0].Heading;
        page.ToggleGroup(page.Groups[0]);

        page.SetGrouping(ProjectGrouping.State);

        Assert.Equal((heading, true), (page.Groups[0].Heading, page.Groups[0].IsCollapsed));
    }
}
