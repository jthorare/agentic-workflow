using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Start;

namespace Aw.Sindri.Ui.Tests.Start;

/// <summary>The Start form's state over time (U5', review 351): what an answer leaves behind, what a late read or answer may not change, and what Start waits for.</summary>
[Lane(TestLane.Unit)]
public sealed class StartFormStateTests
{
    private const string Approved = "approved";
    private readonly StartFormFixture _fixture = new();

    private static CancellationToken Token => StartFormFixture.Token;

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AgenticWorkflow.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("no repo root above the test binaries");
    }

    private static JsonNode ItemFact(StartInstanceRequest sent) => JsonNode.Parse(sent.Facts["fact:item"].Content.Span)!; // why: the content is JSON

    // ---- what an answer leaves behind ----

    [Fact]
    public async Task After_an_existing_answer_a_new_item_resets_the_sent_state_the_outcome_and_the_new_attempt_offer()
    {
        _fixture.Answers(existing: true, finished: true);
        var form = await _fixture.Opened("T-1");
        form.Fields[0].Value = "the spec";
        await form.Submit(Token);
        Assert.Equal((false, true, true), (form.CanStart, form.HasOutcome, form.CanStartNewAttempt));

        form.ItemId = "T-2";

        Assert.Equal((true, false, false, false), (form.CanStart, form.HasOutcome, form.OffersNewAttempt, form.CanStartNewAttempt));
    }

    [Fact]
    public async Task After_an_existing_answer_another_recipe_resets_the_sent_state_the_outcome_and_the_new_attempt_offer()
    {
        _fixture.Rows((StartFormFixture.Step, Approved, 2), (StartFormFixture.Triage, Approved, 2));
        _fixture.Answers(existing: true, finished: true);
        var form = await _fixture.Opened("T-1");
        form.Fields[0].Value = "the spec";
        await form.Submit(Token);

        await form.Choose(form.Recipes.Single(r => r.Id == StartFormFixture.Triage), Token);

        Assert.Equal((true, false, false), (form.CanStart, form.HasOutcome, form.CanStartNewAttempt));
    }

    [Fact]
    public async Task A_new_attempt_is_never_sent_for_an_item_that_was_not_shown_as_finished()
    {
        _fixture.Answers(existing: true, finished: true);
        var form = await _fixture.Opened("T-1");
        form.Fields[0].Value = "the spec";
        await form.Submit(Token);
        form.ItemId = "T-2";

        await form.StartNewAttempt(Token);

        _ = Assert.Single(_fixture.Server.Started);
    }

    // ---- Start waits for the inputs ----

    [Fact]
    public async Task Start_is_off_while_the_chosen_recipes_inputs_are_read_and_a_press_sends_nothing()
    {
        var form = await _fixture.Form();
        await form.Open("T-1", Token);
        var gate = _fixture.Server.RecipeGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var choosing = form.Choose(form.Recipes[0], Token);
        await form.Submit(Token);

        Assert.Equal((false, 0), (form.CanStart, _fixture.Server.Started.Count));
        gate.SetResult();
        await choosing;
        Assert.True(form.CanStart);
    }

    // ---- stale reads and answers ----

    [Fact]
    public async Task A_recipe_list_read_that_answers_after_the_form_was_closed_and_opened_again_is_dropped()
    {
        var form = await _fixture.Form();
        var first = _fixture.Server.RecipesGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstOpen = form.Open("A", Token);
        form.Close();
        var second = _fixture.Server.RecipesGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondOpen = form.Open("B", Token);

        first.SetResult();
        await firstOpen;

        Assert.Equal((true, "B", 0), (form.IsLoading, form.ItemId, form.Recipes.Count));
        second.SetResult();
        await secondOpen;
        Assert.Equal((false, true), (form.IsLoading, form.Recipes.Count > 0));
    }

    [Fact]
    public async Task A_recipe_read_that_answers_after_another_recipe_was_chosen_is_dropped()
    {
        _fixture.Rows((StartFormFixture.Step, Approved, 2), (StartFormFixture.Triage, Approved, 2));
        var form = await _fixture.Form();
        await form.Open("T-1", Token);
        var late = _fixture.Server.RecipeGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var choosingStep = form.Choose(form.Recipes.Single(r => r.Id == StartFormFixture.Step), Token);
        var current = _fixture.Server.RecipeGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var choosingTriage = form.Choose(form.Recipes.Single(r => r.Id == StartFormFixture.Triage), Token);

        late.SetResult();
        await choosingStep;

        Assert.Equal((StartFormFixture.Triage, 0, false), (form.Recipe?.Id, form.Fields.Count, form.CanStart));
        current.SetResult();
        await choosingTriage;
        Assert.Equal((true, true), (form.Fields.Count > 0, form.CanStart));
    }

    [Fact]
    public async Task Closing_the_form_while_a_start_is_in_flight_opens_no_frame_when_the_answer_arrives()
    {
        var form = await _fixture.Opened("T-1");
        form.Fields[0].Value = "the spec";
        var opened = 0;
        form.Started += (_, _) => opened++;
        var gate = _fixture.Server.StartGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sending = form.Submit(Token);

        form.Close();
        gate.SetResult();
        await sending;

        Assert.Equal((0, false, false), (opened, form.IsOpen, form.OpenedFrame));
    }

    [Fact]
    public async Task An_answer_to_a_send_made_before_the_item_changed_opens_its_frame_and_says_nothing_about_the_new_item()
    {
        _fixture.Answers(existing: true, finished: true);
        var form = await _fixture.Opened("T-1");
        form.Fields[0].Value = "the spec";
        var opened = 0;
        form.Started += (_, _) => opened++;
        var gate = _fixture.Server.StartGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sending = form.Submit(Token);

        form.ItemId = "T-2";
        gate.SetResult();
        await sending;

        Assert.Equal((1, false, false, true), (opened, form.HasOutcome, form.OffersNewAttempt, form.CanStart));
    }

    // ---- recovery ----

    [Fact]
    public async Task A_recipe_that_could_not_be_read_can_be_chosen_again_and_then_starts()
    {
        _fixture.Rows((StartFormFixture.Step, Approved, 2));
        var form = await _fixture.Form();
        await form.Open("T-1", Token);
        var good = _fixture.Server.WorkflowAnswer;
        _fixture.Server.WorkflowAnswer = RouteAnswer.Problem("workflow-recipe", "not-found");
        await form.Choose(form.Recipes[0], Token);
        Assert.Equal((false, true), (form.CanStart, form.HasNote));

        _fixture.Server.WorkflowAnswer = good;
        await form.Choose(form.Recipes[0], Token);

        Assert.Equal((true, false, true), (form.CanStart, form.HasNote, form.Fields.Count > 0));
    }

    [Fact]
    public async Task A_recipe_chosen_while_the_api_was_down_can_be_chosen_again_when_it_is_back()
    {
        _fixture.Rows((StartFormFixture.Step, Approved, 2));
        var api = (Uri?)StartFormFixture.Api;
        var catalog = new ViewModels.Controls.CatalogViewModel(new FakeClipboard(), _fixture.Clock);
        catalog.Show(StartFormFixture.Api, _fixture.Server, (await _fixture.Server.Commands(StartFormFixture.Api, Token)).Value);
        var form = new StartFormViewModel(catalog, () => api, _fixture.Server, _fixture.Files);
        await form.Open("T-1", Token);
        api = null;
        await form.Choose(form.Recipes[0], Token);
        Assert.False(form.CanStart);

        api = StartFormFixture.Api;
        await form.Choose(form.Recipes[0], Token);

        Assert.Equal((true, true), (form.CanStart, form.Fields.Count > 0));
    }

    // ---- the item fact (review 351 correctness-reviewer-1) ----

    [Fact]
    public async Task A_picked_item_with_a_repo_is_sent_as_the_item_fact_with_its_id_and_repo()
    {
        _fixture.Rows((StartFormFixture.Triage, Approved, 2));
        var form = await _fixture.Opened();
        form.Fields[0].Value = "the scope";
        var picked = form.Items.First(i => i.Repo is not null);
        form.PickedItem = picked;

        await form.Submit(Token);

        var fact = ItemFact(Assert.Single(_fixture.Server.Started));
        Assert.Equal((picked.Id, picked.Repo), ((string?)fact["id"], (string?)fact["repo"]));
    }

    [Fact]
    public async Task A_typed_item_the_queue_does_not_list_is_sent_as_the_item_fact_with_its_id_alone()
    {
        _fixture.Rows((StartFormFixture.Triage, Approved, 2));
        var form = await _fixture.Opened("T-1");
        form.Fields[0].Value = "the scope";

        await form.Submit(Token);

        var fact = ItemFact(Assert.Single(_fixture.Server.Started));
        Assert.Equal(("T-1", 1), ((string?)fact["id"], fact.AsObject().Count));
    }

    // workflow.item's claim step reads the repo out of fact:item (effect.claim.ref): a start from the form must give it one.
    [Fact]
    public async Task The_shipped_item_recipe_claims_a_ref_from_its_item_fact_and_the_form_gives_that_fact_a_repo()
    {
        var recipe = await File.ReadAllTextAsync(Path.Combine(RepoRoot(), "recipes", "sindri", "workflow.item.yaml"), Token);
        Assert.Contains("effect.claim.ref", recipe, StringComparison.Ordinal);
        Assert.Matches(@"key:\s*fact:item\b[^}]*type:\s*item\b", recipe);

        _fixture.Rows(("workflow.item", Approved, 2));
        var item = await _fixture.Form();
        await item.Open(itemId: null, Token);
        await item.Choose(item.Recipes[0], Token);
        item.PickedItem = item.Items.First(i => i.Repo is not null);
        await item.Submit(Token);

        Assert.False(string.IsNullOrEmpty((string?)ItemFact(Assert.Single(_fixture.Server.Started))["repo"]));
    }

    // ---- the 1 MiB boundary, and the reads around the form ----

    [Fact]
    public async Task A_text_of_exactly_one_mebibyte_is_sent_and_one_byte_more_is_refused()
    {
        var form = await _fixture.Opened("T-1");
        form.Fields[0].Value = new string('a', StartFieldViewModel.MaxBytes + 1);
        await form.Submit(Token);
        Assert.Equal((Labels.StartTooLarge(form.Fields[0].Label), 0), (form.ErrorDetail, _fixture.Server.Started.Count));

        form.Fields[0].Value = new string('a', StartFieldViewModel.MaxBytes);
        await form.Submit(Token);

        Assert.Equal(StartFieldViewModel.MaxBytes, Assert.Single(_fixture.Server.Started).Facts.Values.Single().Content.Length);
    }

    [Fact]
    public async Task A_queue_that_cannot_be_read_leaves_the_item_to_be_typed_and_the_form_startable()
    {
        _fixture.Server.QueueView = RouteAnswer.Problem(System.Net.HttpStatusCode.ServiceUnavailable, "SND-API-503", "the queue is not available");
        var form = await _fixture.Opened("T-1");
        form.Fields[0].Value = "the spec";

        await form.Submit(Token);

        Assert.Equal((0, 1), (form.Items.Count, _fixture.Server.Started.Count));
    }

    [Fact]
    public async Task Editing_the_item_or_a_field_clears_a_served_refusal()
    {
        var form = await _fixture.Opened("T-1");
        form.Fields[0].Value = "the spec";
        _fixture.Server.StartAnswer = RouteAnswer.Problem("workflow-instances", "start-missing-input");
        await form.Submit(Token);
        Assert.True(form.HasError);
        form.ItemId = "T-2";
        Assert.False(form.HasError);

        await form.Submit(Token);
        Assert.True(form.HasError);
        form.Fields[0].Value = "the spec, again";

        Assert.Equal((false, string.Empty, string.Empty), (form.HasError, form.ErrorCode, form.ErrorDetail));
    }

    [Fact]
    public async Task A_link_is_sent_as_a_url_file_and_a_payload_as_a_json_file_under_the_key_without_its_prefix()
    {
        _fixture.Rows((StartFormFixture.Step, Approved, 2));
        _fixture.Bend(StartFormFixture.Step, input => input["type"] = "link");
        var link = await _fixture.Opened("T-1");
        link.Fields[0].Value = "https://example.test/spec";
        _fixture.Bend(StartFormFixture.Step, input => input["type"] = "payload");
        var payload = await _fixture.Opened("T-1");
        payload.Fields[0].Value = "{}";

        await link.Submit(Token);
        await payload.Submit(Token);

        var names = _fixture.Server.Started.Select(s => s.Facts.Values.Single().Name).ToList();
        Assert.Equal([$"{link.Fields[0].Key["fact:".Length..]}.url", $"{payload.Fields[0].Key["fact:".Length..]}.json"], names);
    }

    // ---- why Start is off, and what is announced ----

    [Fact]
    public async Task A_start_that_is_off_says_why_and_the_reason_is_announced()
    {
        _fixture.Rows((StartFormFixture.Step, Approved, 2));
        var form = await _fixture.Form();
        await form.Open("T-1", Token);
        var gate = _fixture.Server.RecipeGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var choosing = form.Choose(form.Recipes[0], Token);
        Assert.Equal((Labels.StartWhyReading, true, Labels.StartReadingInputs), (form.StartHelp, form.IsBusy, form.BusyText));
        gate.SetResult();
        await choosing;
        Assert.Equal((string.Empty, false), (form.StartHelp, form.IsBusy));

        _fixture.Server.WorkflowAnswer = RouteAnswer.Problem("workflow-recipe", "not-found");
        var failing = await _fixture.Form();
        await failing.Open("T-1", Token);
        await failing.Choose(failing.Recipes[0], Token);
        Assert.Equal((failing.Note, failing.Note, true), (failing.StartHelp, failing.Announcement, failing.HasNote));
    }

    [Fact]
    public async Task A_catalog_that_arrives_while_the_form_is_open_makes_start_ready()
    {
        _fixture.Rows((StartFormFixture.Step, Approved, 2));
        var catalog = new ViewModels.Controls.CatalogViewModel(new FakeClipboard(), _fixture.Clock);
        var form = new StartFormViewModel(catalog, () => StartFormFixture.Api, _fixture.Server, _fixture.Files);
        await form.Open("T-1", Token);
        await form.Choose(form.Recipes[0], Token);
        Assert.Equal((false, Labels.StartNoCatalog), (form.IsReady, form.Readiness));

        catalog.Show(StartFormFixture.Api, _fixture.Server, (await _fixture.Server.Commands(StartFormFixture.Api, Token)).Value);

        Assert.Equal((true, true), (form.IsReady, form.CanStart));
    }

    [Fact]
    public async Task Typing_an_item_the_list_does_not_name_clears_the_list_pick()
    {
        var form = await _fixture.Opened();
        form.PickedItem = form.Items[0];

        form.ItemId = "T-9";

        Assert.Null(form.PickedItem);
    }

    [Fact]
    public async Task A_refused_new_attempt_leaves_the_offer_standing()
    {
        _fixture.Answers(existing: true, finished: true);
        var form = await _fixture.Opened("T-1");
        form.Fields[0].Value = "the spec";
        await form.Submit(Token);
        _fixture.Server.StartAnswer = RouteAnswer.Problem("workflow-instances", "start-missing-input");

        await form.StartNewAttempt(Token);

        Assert.Equal((true, true), (form.HasError, form.CanStartNewAttempt));
    }

    // ---- review 351 run 2 ----

    [Fact]
    public async Task A_recipe_read_for_an_earlier_choice_of_the_same_recipe_is_dropped()
    {
        _fixture.Rows((StartFormFixture.Step, Approved, 2), (StartFormFixture.Triage, Approved, 2));
        var form = await _fixture.Form();
        await form.Open("T-1", Token);
        var step = form.Recipes.Single(r => r.Id == StartFormFixture.Step);
        var first = _fixture.Server.RecipeGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var choosingA1 = form.Choose(step, Token);
        var second = _fixture.Server.RecipeGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var choosingB = form.Choose(form.Recipes.Single(r => r.Id == StartFormFixture.Triage), Token);
        var third = _fixture.Server.RecipeGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var choosingA2 = form.Choose(step, Token);

        first.SetResult();
        await choosingA1;

        Assert.Equal((0, false), (form.Fields.Count, form.CanStart));
        second.SetResult();
        await choosingB;
        Assert.Equal((0, false), (form.Fields.Count, form.CanStart));
        third.SetResult();
        await choosingA2;
        Assert.Equal((true, true), (form.Fields.Count > 0, form.CanStart));
    }

    [Fact]
    public async Task A_recipe_that_could_not_be_read_is_no_longer_the_selected_one_so_choosing_it_again_is_a_change()
    {
        _fixture.Rows((StartFormFixture.Step, Approved, 2));
        _fixture.Server.WorkflowAnswer = RouteAnswer.Problem("workflow-recipe", "not-found");
        var form = await _fixture.Form();
        await form.Open("T-1", Token);

        await form.Choose(form.Recipes[0], Token);

        Assert.Null(form.Recipe);
    }

    [Fact]
    public async Task A_send_with_no_api_is_refused_as_not_running_and_sends_nothing()
    {
        var api = (Uri?)StartFormFixture.Api;
        var catalog = new ViewModels.Controls.CatalogViewModel(new FakeClipboard(), _fixture.Clock);
        catalog.Show(StartFormFixture.Api, _fixture.Server, (await _fixture.Server.Commands(StartFormFixture.Api, Token)).Value);
        var form = new StartFormViewModel(catalog, () => api, _fixture.Server, _fixture.Files);
        await form.Open("T-1", Token);
        await form.Choose(form.Recipes[0], Token);
        form.Fields[0].Value = "the spec";
        api = null;

        await form.Submit(Token);

        Assert.Equal((true, 0, form.IsSending), (form.HasError, _fixture.Server.Started.Count, false));
        Assert.Contains("SND-UI", form.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_answer_for_a_new_instance_after_an_edit_opens_its_frame_and_says_which_instance_started()
    {
        var form = await _fixture.Opened("T-1");
        form.Fields[0].Value = "the spec";
        var opened = 0;
        form.Started += (_, _) => opened++;
        var gate = _fixture.Server.StartGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sending = form.Submit(Token);

        form.ItemId = "T-2";
        gate.SetResult();
        await sending;

        var address = JsonNode.Parse(GoldenFiles.Api("workflow-instances-start", "started"))!["instance"]!["address"]!.GetValue<string>(); // why: the golden has an instance
        Assert.Equal((1, true, true, Labels.StartedBehind(address)), (opened, form.IsOpen, form.OpenedFrame, form.Announcement));
    }

    [Fact]
    public async Task A_refusal_for_a_send_made_before_an_edit_is_dropped_and_does_not_move_focus()
    {
        var form = await _fixture.Opened("T-1");
        form.Fields[0].Value = "the spec";
        _fixture.Server.StartAnswer = RouteAnswer.Problem("workflow-instances", "start-missing-input");
        var gate = _fixture.Server.StartGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sending = form.Submit(Token);

        form.ItemId = "T-2";
        gate.SetResult();
        await sending;

        Assert.Equal((false, 0), (form.HasError, form.ErrorFocusRequests));
    }

    [Fact]
    public async Task A_link_is_sent_without_the_spaces_around_it()
    {
        _fixture.Rows((StartFormFixture.Step, Approved, 2));
        _fixture.Bend(StartFormFixture.Step, input => input["type"] = "link");
        var form = await _fixture.Opened("T-1");
        form.Fields[0].Value = "  https://example.test/spec \n";

        await form.Submit(Token);

        Assert.Equal("https://example.test/spec", System.Text.Encoding.UTF8.GetString(Assert.Single(_fixture.Server.Started).Facts.Values.Single().Content.Span));
    }

    [Fact]
    public async Task A_file_of_exactly_one_mebibyte_is_sent_and_one_byte_more_is_refused()
    {
        _fixture.Rows((StartFormFixture.Project, Approved, 12));
        var form = await _fixture.Opened("T-1");
        form.Fields.Single(f => f.IsPayload).Value = "{}";
        var file = form.Fields.Single(f => f.IsFile);
        _fixture.Files.Queue(new PickedFile("prd.md", StartFieldViewModel.MaxBytes + 1, ReadOnlyMemory<byte>.Empty));
        await file.Pick(Token);
        await form.Submit(Token);
        Assert.Equal((Labels.StartTooLarge(file.Label), 0), (form.ErrorDetail, _fixture.Server.Started.Count));

        _fixture.Files.Queue(new PickedFile("prd.md", StartFieldViewModel.MaxBytes, new byte[StartFieldViewModel.MaxBytes]));
        await file.Pick(Token);
        await form.Submit(Token);

        Assert.Equal(StartFieldViewModel.MaxBytes, Assert.Single(_fixture.Server.Started).Facts["fact:prd"].Content.Length);
    }
}
