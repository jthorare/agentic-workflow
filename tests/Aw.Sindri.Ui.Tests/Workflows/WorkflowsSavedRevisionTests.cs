using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>Saving an edit on a live workflow (#3303): the sheet keeps the saved text, a line says the new revision is not live yet, Make live turns it on, and Run now says which revision it runs.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowsSavedRevisionTests
{
#pragma warning disable MA0136 // a raw string with line ends is the readable form of this JSON
    private const string Blocks = """
        {"groups":[
          {"group":"Triggers","blocks":[{"type":"manual","title":"Manual","isTrigger":true,"config":[],"outputs":{},"branches":["out"],"exampleConfig":null,"exampleSpec":null}]},
          {"group":"AI","blocks":[{"type":"run_agent","title":"Run an agent","isTrigger":false,
            "config":[{"name":"prompt","type":"text","required":true},{"name":"strict","type":"boolean","required":false}],
            "outputs":{"text":"text"},"branches":["out"],"exampleConfig":null,"exampleSpec":null}]}
        ]}
        """;
#pragma warning restore MA0136

    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // What Sindri holds: the plain read shows the live revision (the newest when off), ?revision=latest the newest. Revision 3 is the one with the text saved from the sheet.
    private void Serve(int? active, int latest)
    {
        var live = Golden("workflows-slug", "found");
        live["activeRevision"] = active;
        live["revision"] = active ?? latest;
        live["latestRevision"] = latest;
        var newest = Golden("workflows-slug", "found");
        newest["activeRevision"] = active;
        newest["revision"] = latest;
        newest["latestRevision"] = latest;
        if (latest == 3)
        {
            newest["spec"]!["nodes"]![1]!["config"]!["prompt"] = "Check twice";
            newest["summaries"]![1]!["summary"] = "Run agent twice";   // the newest revision's own summary line
            if (active is null)
            {
                live["spec"] = newest["spec"]!.DeepClone();
            }
        }

        _f.Server.WorkflowAnswers[Slug] = new RouteAnswer(HttpStatusCode.OK, live.ToJsonString());
        _f.Server.WorkflowNewestAnswers[Slug] = new RouteAnswer(HttpStatusCode.OK, newest.ToJsonString());
    }

    private async Task<WorkflowsPageViewModel> EditingNode(int? active)
    {
        _f.Server.WorkflowBlocksAnswer = new RouteAnswer(HttpStatusCode.OK, Blocks);
        Serve(active, latest: 2);
        var page = await _f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);
        _ = page.Runs.Graph!.SelectNode("agent");
        await page.Edit(Token);
        return page;
    }

    private async Task<WorkflowsPageViewModel> Saved(int? active)
    {
        var page = await EditingNode(active);
        page.Editor.Fields[0].Text = "Check twice";
        Serve(active, latest: 3);   // what the save makes Sindri hold: the live revision is unchanged, the newest is 3
        await page.Save(Token);
        return page;
    }

    [Fact]
    public async Task Saving_a_live_workflow_shows_the_saved_revision()
    {
        var page = await Saved(active: 1);

        Assert.Equal("Check twice", page.Editor.Fields[0].Text);   // not the live revision's "Look for drift"
        Assert.Equal(3, page.Editor.BaseRevision);
    }

    [Fact]
    public async Task Opening_the_workflow_again_shows_the_saved_text_in_the_sheet_and_the_graph()
    {
        _ = await Saved(active: 1);

        var again = await _f.Shown(Token);   // a fresh page, as after a restart: nothing is remembered from the save
        await again.OpenWorkflow(Slug, Token);
        _ = again.Runs.Graph!.SelectNode("agent");
        await again.Edit(Token);

        Assert.Equal("Check twice", again.Editor.Fields[0].Text);
        Assert.Contains("Run agent twice", again.Runs.Graph.StepLine, StringComparison.Ordinal);   // the canvas draws the saved revision too
        Assert.Equal("Saved as revision 3. Not live yet.", again.SavedLineText);
    }

    [Fact]
    public async Task Saving_a_live_workflow_says_it_is_not_live_yet_and_offers_Make_live()
    {
        var page = await Saved(active: 1);

        Assert.Equal(("Saved as revision 3. Not live yet.", true, true), (page.SavedLineText, page.HasSavedLine, page.CanMakeLive));
        Assert.False(page.ShowActivateButton);   // Make live is the one button for it, not Activate next to it
    }

    [Fact]
    public async Task Run_now_says_which_revision_it_runs_while_a_newer_one_is_saved()
    {
        var page = await Saved(active: 1);

        Assert.Equal("Run revision 1 (live)", page.Runs.RunNowText);
    }

    [Fact]
    public async Task Make_live_activates_the_saved_revision()
    {
        var page = await Saved(active: 1);
        Serve(active: 3, latest: 3);

        await page.MakeLive(Token);

        Assert.Equal([Slug], _f.Server.ActivatedWorkflows);
        Assert.Equal((string.Empty, false, false), (page.SavedLineText, page.HasSavedLine, page.CanMakeLive));
        Assert.Equal("Run now", page.Runs.RunNowText);
    }

    [Fact]
    public async Task Saving_a_workflow_that_is_off_says_saved_with_no_buttons()
    {
        var page = await Saved(active: null);

        Assert.Equal(("Saved as revision 3.", true, false), (page.SavedLineText, page.HasSavedLine, page.CanMakeLive));
    }

    [Fact]
    public async Task Opening_another_workflow_drops_the_saved_line()
    {
        var page = await Saved(active: null);

        page.Back();

        Assert.Equal((string.Empty, false), (page.SavedLineText, page.HasSavedLine));
    }

    [Fact]
    public async Task A_workflow_with_no_save_in_this_sheet_and_nothing_newer_has_no_line()
    {
        var page = await EditingNode(active: 2);

        Assert.Equal((string.Empty, false, false), (page.SavedLineText, page.HasSavedLine, page.CanMakeLive));
    }
}
