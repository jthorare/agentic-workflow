using System.Net;
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

    // the list as the next read serves it: the open workflow's row is on (at this revision) or off
    private void ServeRow(int? active)
    {
        var list = Golden("workflows", "list-two");
        list["workflows"]![0]!["activeRevision"] = active;
        list["workflows"]![0]!["latestRevision"] = 3;
        _f.ServeList(list);
    }

    private async Task<WorkflowsPageViewModel> EditingNode(int? active)
    {
        _f.Server.WorkflowBlocksAnswer = new RouteAnswer(HttpStatusCode.OK, Blocks);
        Serve(active, latest: 2);
        var page = await _f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);
        _ = page.Runs.Graph!.SelectNode("agent");
        await page.OpenSettings(Token);
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
        await again.OpenSettings(Token);

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
    public async Task Activate_then_Pause_after_a_save_on_a_workflow_that_is_off_drops_the_saved_line()
    {
        ServeRow(active: null);
        var page = await Saved(active: null);
        ServeRow(active: 3);
        Serve(active: 3, latest: 3);
        await page.Activate(Token);
        ServeRow(active: null);
        Serve(active: null, latest: 3);

        await page.Pause(Token);

        Assert.Equal([Slug], _f.Server.PausedWorkflows);
        Assert.Equal((string.Empty, false), (page.SavedLineText, page.HasSavedLine));   // nothing was saved since the line last showed
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

    // What the ops route answers: the revision the edit made (the golden's ops-applied names revision 2).
    private static RouteAnswer AppliedAs(int revision)
    {
        var applied = Golden("workflows-slug-ops", "ops-applied");
        applied["revision"] = revision;
        return new RouteAnswer(HttpStatusCode.OK, applied.ToJsonString());
    }

    // Saves the sheet's text, then undoes it: the undo writes one revision more (4), and the page reads it.
    private async Task<WorkflowsPageViewModel> SavedThenUndone(int? active)
    {
        WorkflowStepSavedEventArgs? saved = null;
        var page = await EditingNode(active);
        page.StepSaved += (_, e) => saved = e;
        page.Editor.Fields[0].Text = "Check twice";
        Serve(active, latest: 3);
        _f.Server.ApplyWorkflowOpsAnswer = AppliedAs(3);   // the save's own answer names the revision it made: Undo is offered on that one (#4398), not on whatever the reload reads
        await page.Save(Token);
        Serve(active, latest: 4);   // what the undo makes Sindri hold
        Assert.Null(await saved!.Undo());
        return page;
    }

    [Fact]
    public async Task After_an_undo_on_a_workflow_that_is_off_the_line_says_undone_and_the_revision_the_undo_wrote()
    {
        var page = await SavedThenUndone(active: null);

        Assert.Equal("Undone. Saved as revision 4.", page.SavedLineText);
    }

    [Fact]
    public async Task After_an_undo_on_a_live_workflow_the_line_says_undone_and_not_live_yet()
    {
        var page = await SavedThenUndone(active: 1);

        Assert.Equal("Undone. Saved as revision 4. Not live yet.", page.SavedLineText);
    }

    // A canvas Remove with the sheet shut: it writes revision 5, and the page reads it.
    private async Task RemoveOnCanvas(WorkflowsPageViewModel page, int? active)
    {
        page.CloseSettings();
        Serve(active, latest: 5);
        page.GraphEdit.RequestRemove();
        await page.GraphEdit.ConfirmRemove(Token);
    }

    [Fact]
    public async Task On_a_workflow_that_is_off_a_canvas_edit_after_a_save_moves_the_line_to_the_edits_revision()
    {
        var page = await Saved(active: null);

        await RemoveOnCanvas(page, active: null);

        Assert.Equal("Saved as revision 5.", page.SavedLineText);
    }

    [Fact]
    public async Task On_a_workflow_that_is_off_a_canvas_edit_after_an_undo_no_longer_says_undone()
    {
        var page = await SavedThenUndone(active: null);

        await RemoveOnCanvas(page, active: null);

        Assert.Equal("Saved as revision 5.", page.SavedLineText);
    }

    [Fact]
    public async Task On_a_live_workflow_a_canvas_edit_after_an_undo_no_longer_says_undone()
    {
        var page = await SavedThenUndone(active: 1);

        await RemoveOnCanvas(page, active: 1);

        Assert.Equal("Saved as revision 5. Not live yet.", page.SavedLineText);
    }
}
