using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>
/// The undo of Save step (workflows page spec, Ctrl+Z row): a saved step offers "Step saved. Undo (Ctrl+Z)"; Undo saves the graph the save replaced as a NEW revision on the revision read
/// (never a rewind); it is off while an Activate is on the way; a refusal says why and holds the page for a reload.
/// The workflow is the "found" golden: revision 1 live and revision 2 the newest, so the sheet edits revision 2; once it is open the newest read serves revision 3, as a saved step makes it.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowsUndoTests
{
    private readonly WorkflowsFixture _f = new();
    private readonly List<WorkflowStepSavedEventArgs> _saved = [];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static void ServeRevision(WorkflowsFixture f, int revision)
    {
        var newest = Golden("workflows-slug", "found");
        (newest["revision"], newest["latestRevision"]) = (revision, revision);
        newest["spec"]!["nodes"]![1]!["config"]!["prompt"] = "Look twice";
        f.Server.WorkflowNewestAnswers[Slug] = new RouteAnswer(HttpStatusCode.OK, newest.ToJsonString());
    }

    // The run_agent block has more fields than the prompt, so a test never counts on the first one.
    private static ConfigFieldViewModel Prompt(WorkflowsPageViewModel page) => page.Editor.Fields.Single(static f => string.Equals(f.Name, "prompt", StringComparison.Ordinal));

    private async Task<WorkflowsPageViewModel> SheetOnAgent()
    {
        _f.Server.WorkflowBlocksAnswer = RouteAnswer.Api("workflows-blocks", "listed");
        var page = await _f.Shown(Token);
        page.StepSaved += (_, e) => _saved.Add(e);
        await page.OpenWorkflow(Slug, Token);
        Assert.True(page.Runs.Graph!.SelectNode("agent"));
        await page.OpenSettings(Token);
        ServeRevision(_f, 3);   // after the sheet read revision 2: the read after the save finds revision 3
        return page;
    }

    private async Task<WorkflowsPageViewModel> SavedAgent()
    {
        var page = await SheetOnAgent();
        Prompt(page).Text = "Look twice";
        await page.Save(Token);
        return page;
    }

    [Fact]
    public async Task Saving_a_step_offers_the_Step_saved_undo()
    {
        _ = await SavedAgent();

        _ = Assert.Single(_saved);
        Assert.True(_saved[0].CanUndo());
    }

    [Fact]
    public async Task A_refused_save_offers_no_undo()
    {
        var page = await SheetOnAgent();
        _f.Server.ApplyWorkflowOpsAnswer = RouteAnswer.Problem("workflows-slug-ops", "ops-stale");
        Prompt(page).Text = "Look twice";

        await page.Save(Token);

        Assert.Empty(_saved);
    }

    [Fact]
    public async Task Undo_saves_the_previous_graph_as_a_new_revision_on_the_revision_read()
    {
        _ = await SavedAgent();

        var failure = await _saved[0].Undo();

        Assert.Null(failure);
        var put = Assert.Single(_f.Server.ReplacedWorkflows);
        Assert.Equal((Slug, 3), (put.Slug, put.BaseRevision));   // a new revision on revision 3, never a rewind to 2
        Assert.True(JsonNode.DeepEquals(Golden("workflows-slug", "found")["spec"], JsonNode.Parse(put.Spec)));   // the graph revision 1 had
    }

    [Fact]
    public async Task Undo_does_not_offer_another_undo()
    {
        _ = await SavedAgent();

        _ = await _saved[0].Undo();

        _ = Assert.Single(_saved);
    }

    [Fact]
    public async Task Undo_is_off_while_an_Activate_is_on_the_way()
    {
        var page = await SavedAgent();
        _f.Server.WorkflowStateGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var activating = page.Activate(Token);

        Assert.False(_saved[0].CanUndo());
        Assert.NotNull(await _saved[0].Undo());
        Assert.Empty(_f.Server.ReplacedWorkflows);
        _f.Server.WorkflowStateGate.SetResult();
        await activating;
        Assert.True(_saved[0].CanUndo());
    }

    [Fact]
    public async Task An_undo_the_server_refuses_says_why_and_holds_the_page_for_a_reload()
    {
        var page = await SavedAgent();
        _f.Server.ReplaceWorkflowAnswer = RouteAnswer.Problem("workflows-slug", "replace-stale");

        var failure = await _saved[0].Undo();

        Assert.False(string.IsNullOrEmpty(failure));
        Assert.True(page.Editor.NeedsReload);
    }

    [Fact]
    public async Task Saving_a_new_step_offers_the_undo_too()
    {
        var page = await SheetOnAgent();
        page.CloseSettings();
        await page.GraphEdit.BeginAdd(Token);
        var block = page.GraphEdit.Groups.SelectMany(static g => g.Blocks).Single(b => string.Equals(b.Type, "notify", StringComparison.Ordinal));
        await page.GraphEdit.AddStep(block, Token);
        page.Editor.Fields.Single(static f => string.Equals(f.Name, "title", StringComparison.Ordinal)).Text = "Sizes";

        await page.Save(Token);

        _ = Assert.Single(_saved);
    }

    [Fact]
    public async Task A_canvas_edit_after_the_save_turns_Undo_off_and_nothing_is_overwritten()
    {
        var page = await SavedAgent();
        ServeRevision(_f, 4);   // the canvas edit makes revision 4: the editor's base moves on

        await page.Editor.RemoveNode("agent", Token);

        Assert.False(_saved[0].CanUndo());
        Assert.NotNull(await _saved[0].Undo());
        Assert.Empty(_f.Server.ReplacedWorkflows);   // revision 4's edit is not put back to the graph before the save
    }

    [Fact]
    public async Task A_reload_that_finds_a_newer_revision_turns_Undo_off()
    {
        var page = await SavedAgent();
        ServeRevision(_f, 4);   // someone else saved in the window

        await page.ReloadEditor(Token);

        Assert.False(_saved[0].CanUndo());
        Assert.NotNull(await _saved[0].Undo());
        Assert.Empty(_f.Server.ReplacedWorkflows);
    }

    [Fact]
    public async Task A_reload_that_finds_the_same_revision_keeps_Undo_on_and_sends_the_revision_the_save_made()
    {
        var page = await SavedAgent();

        await page.ReloadEditor(Token);

        Assert.True(_saved[0].CanUndo());
        Assert.Null(await _saved[0].Undo());
        Assert.Equal(3, Assert.Single(_f.Server.ReplacedWorkflows).BaseRevision);
    }
}
