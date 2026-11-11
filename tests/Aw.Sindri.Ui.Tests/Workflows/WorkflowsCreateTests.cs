using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Workflows;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>
/// The New workflow form (#2550): a name, a short id filled from it, a starter template. Create stays off with a reason until both are valid, then sets the name on the new workflow and
/// opens its page (the editor sheet closed); a refusal shows Sindri's reason, without its code, in the form.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowsCreateTests
{
    private const string Taken = "The short id 'morning-check' is taken by another workflow. Pick another.";

    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<WorkflowsPageViewModel> Opened()
    {
        _f.ServeList(Golden("workflows", "list-empty"));
        var page = await _f.Shown(Token);
        page.OpenCreate();
        return page;
    }

    [Fact]
    public async Task Opening_the_form_offers_the_three_templates_by_title_with_the_first_picked()
    {
        var page = await Opened();

        Assert.True(page.IsCreating);
        Assert.Equal(["weekday-check", "daily-digest", "manual-notify"], page.Templates.Select(t => t.Name), StringComparer.Ordinal);
        Assert.Equal(["Weekday check", "Daily digest", "Manual notify"], page.Templates.Select(t => t.Title), StringComparer.Ordinal);
        Assert.All(page.Templates, t => Assert.NotEmpty(t.Description));
        Assert.Equal(("weekday-check", "Weekday check"), (page.SelectedTemplate, page.SelectedChoice!.Title));
    }

    // Sindri has no route that lists templates, so the form carries the names GraphTemplates (Aw.Sindri.Core/Graph/Templates/GraphTemplates.cs) accepts.
    // When a template there is renamed, added or removed, this fails: update Labels.WfTemplates with it.
    [Fact]
    public void The_templates_the_form_offers_are_the_three_names_GraphTemplates_accepts() =>
        Assert.Equal(["weekday-check", "daily-digest", "manual-notify"], Labels.WfTemplates.Select(t => t.Name), StringComparer.Ordinal);

    [Theory]
    [InlineData("Daily report", "daily-report")]
    [InlineData("  Daily   report!! ", "daily-report")]
    [InlineData("Rapport été", "rapport-ete")]
    [InlineData("v2 -- check", "v2-check")]
    public async Task The_short_id_follows_the_name_as_it_is_typed(string name, string expected)
    {
        var page = await Opened();

        page.NewName = name;

        Assert.Equal((expected, true, string.Empty), (page.NewSlug, page.CanCreate, page.CreateReasonText));
    }

    [Fact]
    public async Task Once_the_short_id_is_edited_by_hand_the_name_stops_overwriting_it()
    {
        var page = await Opened();
        page.NewName = "Daily";
        page.NewSlug = "my-id";

        page.NewName = "Daily report";

        Assert.Equal("my-id", page.NewSlug);
    }

    [Fact]
    public async Task Emptying_the_short_id_lets_it_follow_the_name_again()
    {
        var page = await Opened();
        page.NewName = "Daily";
        page.NewSlug = "my-id";
        page.NewSlug = string.Empty;

        page.NewName = "Weekly";

        Assert.Equal("weekly", page.NewSlug);
    }

    [Fact]
    public async Task Clearing_the_short_id_makes_it_follow_the_name_again()
    {
        var page = await Opened();
        page.NewName = "Daily";
        page.NewSlug = "my-id";
        page.NewSlug = string.Empty;

        page.NewName = "Weekly";

        Assert.Equal("weekly", page.NewSlug);
    }

    [Theory]
    [InlineData("!!!")]
    [InlineData("🚀")]
    [InlineData("日報")]
    public async Task A_name_with_no_usable_characters_gives_no_short_id_and_asks_for_one(string name)
    {
        var page = await Opened();

        page.NewName = name;

        Assert.Equal((string.Empty, false, "Type a short id: lowercase letters, digits and '-'.", true), (page.NewSlug, page.CanCreate, page.CreateReasonText, page.CreateReasonIsError));
    }

    [Fact]
    public async Task A_hundred_character_name_cuts_the_short_id_at_63_on_a_word_boundary_and_never_ends_in_a_dash()
    {
        var page = await Opened();

        page.NewName = string.Join(' ', Enumerable.Repeat("report", 15))[..100];

        Assert.True(page.NewSlug.Length <= 63);
        Assert.False(page.NewSlug.EndsWith('-'));
        Assert.Equal(string.Join('-', Enumerable.Repeat("report", 9)), page.NewSlug);   // 9 words are 62 characters with their dashes; a tenth would pass 63
        Assert.True(page.CanCreate);
    }

    [Fact]
    public void A_single_unbroken_word_over_63_characters_is_cut_at_63() => Assert.Equal(new string('a', 63), WorkflowSlugs.From(new string('a', 100)));

    [Fact]
    public async Task Before_a_name_is_typed_the_reason_is_a_hint_and_not_an_error()
    {
        var page = await Opened();

        Assert.Equal(("Type a name to create the workflow.", false, false), (page.CreateReasonText, page.CreateReasonIsError, page.CanCreate));
    }

    [Theory]
    [InlineData("-morning", false)]
    [InlineData("Bad Id", false)]
    [InlineData("docs", false)]
    [InlineData("blocks", false)]
    [InlineData("morning-check", true)]
    [InlineData("a", true)]
    public async Task A_short_id_typed_by_hand_is_checked_by_the_slug_rule_and_the_reason_says_short_id_never_slug(string id, bool valid)
    {
        var page = await Opened();
        page.NewName = "Anything";

        page.NewSlug = id;

        Assert.Equal(valid, page.CanCreate);
        Assert.Equal(valid, page.CreateReasonText.Length == 0);
        Assert.DoesNotContain("slug", page.CreateReasonText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(!valid, page.ShortIdInvalid);
    }

    [Fact]
    public async Task A_short_id_over_63_characters_is_not_valid()
    {
        var page = await Opened();
        page.NewName = "Anything";

        page.NewSlug = new string('a', 64);
        Assert.False(page.CanCreate);
        page.NewSlug = new string('a', 63);
        Assert.True(page.CanCreate);
    }

    [Fact]
    public async Task A_short_id_a_listed_workflow_already_has_is_said_before_Create_and_keeps_Create_off()
    {
        _f.ServeList(Golden("workflows", "list-two"));
        var page = await _f.Shown(Token);
        page.OpenCreate();

        page.NewName = "Morning check";

        Assert.Equal(("morning-check", false, "A workflow already uses 'morning-check'; pick another short id.", true), (page.NewSlug, page.CanCreate, page.CreateReasonText, page.CreateReasonIsError));
    }

    [Fact]
    public async Task A_short_id_only_an_archived_workflow_holds_is_said_taken_before_Create_and_the_page_list_stays_live_only()
    {
        _f.Server.AllWorkflowsAnswer = new RouteAnswer(HttpStatusCode.OK, """{"workflows":[{"slug":"old-report","name":"Old report","activeRevision":null,"latestRevision":1,"changedBy":"owner","changedAt":"2026-01-01T00:00:00Z","archived":true}]}""");
        var page = await Opened();
        await page.HeldRead;

        page.NewName = "Old report";

        Assert.Equal(("old-report", false, "A workflow already uses 'old-report'; pick another short id.", true), (page.NewSlug, page.CanCreate, page.CreateReasonText, page.CreateReasonIsError));
        Assert.Empty(page.Rows);
        Assert.Equal(1, _f.Server.AllWorkflowsCalls);
    }

    [Fact]
    public async Task Create_sends_the_short_id_and_template_once_then_sets_the_name_and_opens_the_page_with_the_editor_closed()
    {
        var page = await Opened();
        page.NewName = "Morning check";
        page.SelectedTemplate = "manual-notify";

        await page.Create(Token);

        Assert.Equal([(Slug, "manual-notify")], _f.Server.CreatedWorkflows);
        var rename = Assert.Single(_f.Server.AppliedWorkflowOps);
        Assert.Equal((Slug, 1), (rename.Slug, rename.BaseRevision));
        Assert.Equal("[{\"op\":\"set_meta\",\"name\":\"Morning check\"}]", rename.Ops);
        Assert.Equal((false, true, false), (page.IsCreating, page.IsRuns, page.IsEditing));
        Assert.Equal((string.Empty, false), (page.CreateError, page.IsCreateSending));
    }

    [Fact]
    public async Task A_name_with_quotes_reaches_the_ops_as_valid_JSON()
    {
        var page = await Opened();
        page.NewName = "The \"daily\" one";
        page.NewSlug = Slug;

        await page.Create(Token);

        var rename = Assert.Single(_f.Server.AppliedWorkflowOps);
        Assert.Equal("The \"daily\" one", System.Text.Json.JsonDocument.Parse(rename.Ops).RootElement[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task A_name_that_could_not_be_set_keeps_the_workflow_opens_it_and_says_so_on_the_page()
    {
        var page = await Opened();
        _f.Server.ApplyWorkflowOpsAnswer = RouteAnswer.Problem(HttpStatusCode.Conflict, "SND-GRAPH-007", "The workflow changed since revision 1");
        page.NewName = "Morning check";

        await page.Create(Token);

        Assert.Equal((true, false), (page.IsRuns, page.IsEditing));
        Assert.Equal("Created, but the name couldn't be set: The workflow changed since revision 1", page.Runs.NoticeText);
    }

    [Fact]
    public async Task Create_reads_the_list_again_so_the_new_workflow_is_in_it()
    {
        var page = await Opened();
        page.NewName = "Morning check";
        var reads = _f.Server.WorkflowsCalls;

        await page.Create(Token);

        Assert.True(_f.Server.WorkflowsCalls > reads);
    }

    [Fact]
    public async Task A_refusal_shows_the_reason_without_its_code_keeps_what_was_typed_and_opens_nothing()
    {
        var page = await Opened();
        _f.Server.CreateWorkflowAnswer = RouteAnswer.Problem(HttpStatusCode.Conflict, "SND-GRAPH-009", Taken);
        page.NewName = "Morning check";

        await page.Create(Token);

        Assert.Equal("A workflow already uses 'morning-check'; pick another short id.", page.CreateError);
        Assert.DoesNotContain("SND-", page.CreateError, StringComparison.Ordinal);
        Assert.Equal("SND-GRAPH-009", page.CreateErrorCode);
        Assert.Equal((true, "Morning check", Slug, false, false, false), (page.IsCreating, page.NewName, page.NewSlug, page.IsRuns, page.IsEditing, page.IsCreateSending));
        Assert.False(page.CanCreate);   // the same short id would be refused again
        Assert.True(page.ShortIdInvalid);
        Assert.Empty(_f.Server.AppliedWorkflowOps);
    }

    [Fact]
    public async Task Typing_again_clears_the_refusal()
    {
        var page = await Opened();
        _f.Server.CreateWorkflowAnswer = RouteAnswer.Problem(HttpStatusCode.Conflict, "SND-GRAPH-009", Taken);
        page.NewName = "Morning check";
        await page.Create(Token);

        page.NewName = "Evening check";

        Assert.Equal((string.Empty, string.Empty), (page.CreateError, page.CreateErrorCode));
    }

    [Fact]
    public async Task A_second_press_while_the_create_is_on_the_way_sends_nothing()
    {
        var page = await Opened();
        _f.Server.CreateWorkflowGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        page.NewName = "Morning check";

        var first = page.Create(Token);
        await page.Create(Token);
        _f.Server.CreateWorkflowGate.SetResult();
        await first;

        _ = Assert.Single(_f.Server.CreatedWorkflows);
    }

    [Fact]
    public async Task Create_with_an_invalid_short_id_sends_nothing()
    {
        var page = await Opened();
        page.NewName = "Anything";
        page.NewSlug = "Not Valid";

        await page.Create(Token);

        Assert.Empty(_f.Server.CreatedWorkflows);
    }

    [Fact]
    public async Task Cancel_closes_the_form_and_forgets_the_name_the_short_id_and_the_refusal()
    {
        var page = await Opened();
        _f.Server.CreateWorkflowAnswer = RouteAnswer.Problem(HttpStatusCode.Conflict, "SND-GRAPH-009", Taken);
        page.NewName = "Morning check";
        await page.Create(Token);

        page.CancelCreate();

        Assert.Equal((false, string.Empty, string.Empty, string.Empty), (page.IsCreating, page.NewName, page.NewSlug, page.CreateError));
    }

    [Fact]
    public async Task New_workflow_is_reachable_with_workflows_listed_and_the_form_opens_above_them()
    {
        _f.ServeList(Golden("workflows", "list-two"));
        var page = await _f.Shown(Token);

        page.OpenCreate();

        Assert.Equal((true, true, false, true), (page.IsCreating, page.ShowsCreateForm, page.ShowsEmpty, page.CanOpenCreate));
        Assert.Equal(2, page.Rows.Count);
    }

    [Fact]
    public async Task While_the_views_are_stalled_the_list_says_it_cannot_load_and_shows_no_empty_sentence_or_form()
    {
        _f.ServeList(Golden("workflows", "list-empty"));
        var page = await _f.Shown(Token);
        page.SetViewsStalled(stalled: true, rebuild: null);

        page.OpenCreate();

        Assert.Equal((true, false, false, false, "The lists are stalled, so this may be out of date."), (page.ViewsStalled, page.ShowsEmpty, page.ShowsCreateForm, page.CanOpenCreate, page.StalledText));
        Assert.Equal("The lists are stalled, so this may be out of date.", page.NewWorkflowTip);
        Assert.False(page.IsCreating);
    }

    [Fact]
    public async Task A_stall_hides_the_form_and_the_form_comes_back_with_what_was_typed()
    {
        _f.ServeList(Golden("workflows", "list-empty"));
        var page = await _f.Shown(Token);
        page.OpenCreate();
        page.NewName = "Morning check";
        page.NewSlug = "my-own-id";

        page.SetViewsStalled(stalled: true, rebuild: null);
        Assert.False(page.ShowsCreateForm);
        page.SetViewsStalled(stalled: false, rebuild: null);

        Assert.Equal((true, "Morning check", "my-own-id"), (page.ShowsCreateForm, page.NewName, page.NewSlug));
    }

    [Fact]
    public async Task When_the_stall_clears_the_empty_state_and_New_workflow_come_back()
    {
        _f.ServeList(Golden("workflows", "list-empty"));
        var page = await _f.Shown(Token);
        page.SetViewsStalled(stalled: true, rebuild: null);

        page.SetViewsStalled(stalled: false, rebuild: null);

        Assert.Equal((false, true, true, null), (page.ViewsStalled, page.ShowsEmpty, page.CanOpenCreate, page.NewWorkflowTip));
    }
}
