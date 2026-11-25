using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>Done with a changed field in the step sheet asks first (#3655): typed text survives Done until the person picks Discard.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowsDoneAsksTests
{
#pragma warning disable MA0136 // a raw string with line ends is the readable form of this JSON
    private const string Blocks = """
        {"groups":[
          {"group":"Triggers","blocks":[{"type":"manual","title":"Manual","isTrigger":true,"config":[],"outputs":{},"branches":["out"],"exampleConfig":null,"exampleSpec":null}]},
          {"group":"AI","blocks":[{"type":"run_agent","title":"Run an agent","isTrigger":false,
            "config":[{"name":"prompt","type":"text","required":true}],
            "outputs":{"text":"text"},"branches":["out"],"exampleConfig":null,"exampleSpec":null}]}
        ]}
        """;
#pragma warning restore MA0136

    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<WorkflowsPageViewModel> WithATypedField()
    {
        _f.Server.WorkflowBlocksAnswer = new RouteAnswer(HttpStatusCode.OK, Blocks);
        var page = await _f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);
        _ = page.Runs.Graph!.SelectNode("agent");
        await page.OpenSettings(Token);
        page.Editor.Fields[0].Text = "Check twice";
        return page;
    }

    [Fact]
    public async Task Done_with_a_changed_field_asks_and_keeps_the_typed_text_and_the_sheet()
    {
        var page = await WithATypedField();

        page.RequestDone();

        Assert.True(page.AsksSaveFirst);
        Assert.True(page.IsEditing);
        Assert.True(page.IsSettingsOpen);
        Assert.Equal("Check twice", page.Editor.Fields[0].Text);
    }

    [Fact]
    public async Task Discard_leaves_edit_mode_and_drops_the_question()
    {
        var page = await WithATypedField();
        page.RequestDone();

        page.DiscardAndLeave();

        Assert.False(page.IsEditing);
        Assert.False(page.AsksSaveFirst);
    }

    [Fact]
    public async Task Keep_editing_closes_the_question_and_keeps_the_text()
    {
        var page = await WithATypedField();
        page.RequestDone();

        page.KeepEditing();

        Assert.False(page.AsksSaveFirst);
        Assert.True(page.IsSettingsOpen);
        Assert.Equal("Check twice", page.Editor.Fields[0].Text);
    }

    [Fact]
    public async Task Done_with_no_changed_field_leaves_at_once()
    {
        var page = await WithATypedField();
        page.Editor.Fields[0].Text = "Look for drift";   // typed back to what it was

        page.RequestDone();

        Assert.False(page.AsksSaveFirst);
        Assert.False(page.IsEditing);
    }

    [Fact]
    public async Task Save_in_the_question_leaves_edit_mode_when_the_save_worked()
    {
        var page = await WithATypedField();
        page.RequestDone();

        await page.SaveAndLeave(Token);

        _ = Assert.Single(_f.Server.AppliedWorkflowOps);
        Assert.False(page.IsEditing);
        Assert.False(page.AsksSaveFirst);
    }

    [Fact]
    public async Task A_failed_save_in_the_question_keeps_edit_mode_the_sheet_and_the_text()
    {
        var page = await WithATypedField();
        _f.Server.ApplyWorkflowOpsAnswer = RouteAnswer.Problem("workflows-slug-ops", "ops-stale");
        page.RequestDone();

        await page.SaveAndLeave(Token);

        Assert.True(page.IsEditing);
        Assert.True(page.IsSettingsOpen);
        Assert.True(page.Editor.NeedsReload);
        Assert.Equal("Check twice", page.Editor.Fields[0].Text);
    }

    [Fact]
    public async Task An_archived_workflow_says_Archived_and_does_not_offer_Run_now()
    {
        var golden = Golden("workflows-slug", "found");
        golden["archivedAt"] = "2026-01-01T00:00:00Z";
        _f.Server.WorkflowAnswers[Slug] = new RouteAnswer(HttpStatusCode.OK, golden.ToJsonString());
        var page = await _f.Shown(Token);

        await page.OpenWorkflow(Slug, Token);

        Assert.Equal("\u25cb Archived", page.DetailStateText);
        Assert.Equal(("Archived", "\u25cb", "neutral"), (page.DetailChipWord, page.DetailChipGlyph, page.DetailChipTone));
        Assert.False(page.Runs.ShowsRunNow);
    }

    [Fact]
    public async Task While_the_question_shows_the_sheets_own_Save_and_Close_row_is_hidden_and_it_returns_after()
    {
        var page = await WithATypedField();
        Assert.True(page.ShowsEditorButtons);

        page.RequestDone();
        Assert.Equal((true, false), (page.AsksSaveFirst, page.ShowsEditorButtons));   // the question has its own Save: a second one 60 px below is the same word for the same job

        page.KeepEditing();
        Assert.True(page.ShowsEditorButtons);
    }

    [Fact]
    public async Task Save_in_the_question_with_a_required_field_empty_sends_nothing_and_keeps_edit_mode_the_sheet_and_the_question()
    {
        var page = await WithATypedField();
        page.Editor.Fields[0].Text = string.Empty;   // the required prompt cleared: changed, so Done asks, and Save is off
        page.RequestDone();
        Assert.True(page.AsksSaveFirst);

        await page.SaveAndLeave(Token);

        Assert.Empty(_f.Server.AppliedWorkflowOps);
        Assert.Equal((true, true, true), (page.IsEditing, page.IsSettingsOpen, page.AsksSaveFirst));
    }

    [Fact]
    public async Task The_question_is_forgotten_when_the_sheet_closes_by_its_own_Close()
    {
        var page = await WithATypedField();
        page.RequestDone();

        page.CloseSettings();

        Assert.False(page.AsksSaveFirst);
    }

    [Fact]
    public async Task The_question_is_forgotten_when_the_sheet_opens_on_a_step()
    {
        var page = await WithATypedField();
        page.RequestDone();

        await page.OpenSettings(Token);

        Assert.False(page.AsksSaveFirst);
    }

    [Fact]
    public async Task The_question_is_forgotten_when_another_workflow_opens()
    {
        var page = await WithATypedField();
        page.RequestDone();

        await page.OpenWorkflow(Slug, Token);

        Assert.False(page.AsksSaveFirst);
    }

    [Fact]
    public async Task Escape_with_the_sheet_open_and_a_changed_field_asks_like_Done()
    {
        var page = await WithATypedField();

        page.RequestCloseSheet();

        Assert.Equal((true, true, "Check twice"), (page.AsksSaveFirst, page.IsSettingsOpen, page.Editor.Fields[0].Text));
    }

    [Fact]
    public async Task Escape_with_the_sheet_open_and_nothing_changed_closes_the_sheet_and_stays_in_edit_mode()
    {
        var page = await WithATypedField();
        page.Editor.Fields[0].Text = "Look for drift";   // typed back to what it was

        page.RequestCloseSheet();

        Assert.Equal((false, false, true), (page.AsksSaveFirst, page.IsSettingsOpen, page.IsEditing));
    }

    [Fact]
    public async Task A_second_Done_while_the_question_shows_changes_nothing()
    {
        var page = await WithATypedField();
        page.RequestDone();

        page.RequestDone();

        Assert.Equal((true, true, true, "Check twice"), (page.AsksSaveFirst, page.IsEditing, page.IsSettingsOpen, page.Editor.Fields[0].Text));
    }

    [Fact]
    public async Task Done_while_a_save_is_on_the_way_waits_for_it_and_leaves_when_it_worked()
    {
        var page = await WithATypedField();
        _f.Server.ApplyWorkflowOpsGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var saving = page.Save(Token);

        page.RequestDone();
        Assert.Equal((true, false), (page.IsEditing, page.AsksSaveFirst));   // not asked, not left yet: the save decides

        _f.Server.ApplyWorkflowOpsGate.SetResult();
        await saving;

        Assert.False(page.IsEditing);
    }
}
