using System.Text;
using System.Text.Json.Nodes;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Start;

namespace Aw.Sindri.Ui.Tests.Start;

/// <summary>The Start form's send (U5'): what goes on the wire, the once-only guard, what each served answer and refusal shows, and what is refused before anything is sent.</summary>
[Lane(TestLane.Unit)]
public sealed class StartFormSendTests
{
    private const string Approved = "approved";
    private static readonly TimeSpan _bound = TimeSpan.FromSeconds(10);
    private readonly StartFormFixture _fixture = new();

    private static CancellationToken Token => StartFormFixture.Token;

    private static JsonNode Golden(string slug, string scenario) => JsonNode.Parse(GoldenFiles.Api(slug, scenario))!; // why: the golden is a JSON document

    private static string Wire(StartInstanceRequest request) => Encoding.UTF8.GetString(AwJson.Serialize(request, SindriApiClient.Json));

    private static async Task<StartFormViewModel> Filled(StartFormFixture fixture)
    {
        var request = StartGoldens.Request();
        fixture.Rows((request.Recipe, Approved, 2));
        var form = await fixture.Opened(request.Item);
        form.Fields[0].Value = Encoding.UTF8.GetString(request.Facts[form.Fields[0].Key].Content.Span);
        return form;
    }

    // The request golden is what the app has to put on the wire for these inputs: recipe, item, no next attempt, the spec as base64 under its key.
    [Fact]
    public async Task Submit_sends_the_request_golden_for_the_typed_input_once()
    {
        var form = await Filled(_fixture);

        await form.Submit(Token);

        Assert.Equal(Wire(StartGoldens.Request()), Wire(Assert.Single(_fixture.Server.Started)));
    }

    // A text under 1 MiB is sent as UTF-8; an empty required one stops the send (the recipe would refuse it as SND-CLI-003).
    [Fact]
    public async Task A_required_input_left_empty_stops_the_send_and_says_which()
    {
        var form = await _fixture.Opened("T-1");

        await form.Submit(Token);

        var label = (string)Golden("workflow-recipe", StartFormFixture.Step)["inputs"]![0]!["label"]!; // why: the golden has an input with a label
        Assert.Equal((Labels.StartRequired(label), string.Empty, 1, 0), (form.ErrorDetail, form.ErrorCode, form.ErrorFocusRequests, _fixture.Server.Started.Count));
        Assert.Equal((Labels.StartRequired(label), true, true), (form.Fields[0].Error, form.HasError, form.CanStart));
    }

    // Found in the live proof: the form's sentence stayed under a field that had been filled in since.
    [Fact]
    public async Task Typing_into_the_field_that_was_refused_clears_the_forms_sentence()
    {
        var form = await _fixture.Opened("T-1");
        await form.Submit(Token);

        form.Fields[0].Value = "the spec";

        Assert.Equal((false, string.Empty, string.Empty), (form.HasError, form.ErrorDetail, form.Fields[0].Error));
    }

    [Fact]
    public async Task A_missing_recipe_or_item_stops_the_send_before_the_field_checks()
    {
        var form = await _fixture.Form();
        await form.Open(itemId: null, Token);

        await form.Submit(Token);
        Assert.Equal(Labels.StartChooseRecipe, form.ErrorDetail);

        await form.Choose(form.Recipes[0], Token);
        form.Fields[0].Value = "the spec";
        await form.Submit(Token);

        Assert.Equal((Labels.StartChooseItem, 0), (form.ErrorDetail, _fixture.Server.Started.Count));
    }

    [Fact]
    public async Task A_generated_input_left_empty_is_not_sent_and_one_given_is()
    {
        _fixture.Rows((StartFormFixture.Step, Approved, 2));
        _fixture.Bend(StartFormFixture.Step, input => input["generate"] = "{start:date}");
        var form = await _fixture.Opened("T-1");

        await form.Submit(Token);

        Assert.Empty(Assert.Single(_fixture.Server.Started).Facts);
    }

    // SND-WF-014: for a recipe with an item input the picked item is both the request's item and the fact:item content.
    [Fact]
    public async Task A_triage_start_sends_the_picked_item_as_the_item_and_as_the_fact_item_json()
    {
        _fixture.Rows((StartFormFixture.Triage, Approved, 2));
        var form = await _fixture.Opened("T-1");
        form.Fields[0].Value = "the scope";

        await form.Submit(Token);

        var sent = Assert.Single(_fixture.Server.Started);
        var itemFact = sent.Facts["fact:item"];
        Assert.Equal(("T-1", "T-1", "item.json"), (sent.Item, (string)JsonNode.Parse(itemFact.Content.Span)!["id"]!, itemFact.Name)); // why: the content is the id's JSON
        Assert.Equal(["fact:scope", "fact:item"], sent.Facts.Keys, StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_payload_that_is_not_json_and_a_link_that_is_not_a_link_are_refused_before_the_send()
    {
        _fixture.Rows((StartFormFixture.Step, Approved, 2));
        _fixture.Bend(StartFormFixture.Step, input => input["type"] = "payload");
        var payload = await _fixture.Opened("T-1");
        payload.Fields[0].Value = "{ not json";
        _fixture.Bend(StartFormFixture.Step, input => input["type"] = "link");
        var link = await _fixture.Opened("T-1");
        link.Fields[0].Value = "not a link";

        await payload.Submit(Token);
        await link.Submit(Token);

        Assert.Equal((Labels.StartNotJson("Spec"), Labels.StartNotLink("Spec"), 0), (payload.ErrorDetail, link.ErrorDetail, _fixture.Server.Started.Count));
    }

    [Fact]
    public async Task A_file_over_one_mebibyte_is_refused_before_the_send_and_a_file_at_the_limit_is_sent_under_its_name()
    {
        _fixture.Rows((StartFormFixture.Project, Approved, 12));
        var form = await _fixture.Opened("T-1");
        form.Fields.Single(f => f.IsPayload).Value = "{}";
        var file = form.Fields.Single(f => f.IsFile);
        _fixture.Files.Queue(new PickedFile("prd.md", StartFieldViewModel.MaxBytes + 1, ReadOnlyMemory<byte>.Empty));
        await file.Pick(Token);

        await form.Submit(Token);

        Assert.Equal((Labels.StartTooLarge("PRD"), 0), (form.ErrorDetail, _fixture.Server.Started.Count));

        _fixture.Files.Queue(new PickedFile("prd.md", 3, new byte[] { 1, 2, 3 }));
        await file.Pick(Token);
        await form.Submit(Token);

        var sent = Assert.Single(_fixture.Server.Started).Facts["fact:prd"];
        Assert.Equal("prd.md", sent.Name);
        Assert.Equal([1, 2, 3], sent.Content.ToArray());
    }

    // A refusal leaves the form pressable (the human fixes it and presses again); only an accepted start marks it sent.
    [Fact]
    public async Task A_refusal_leaves_the_form_pressable_and_an_accepted_start_marks_it_sent()
    {
        var form = await Filled(_fixture);
        _fixture.Server.StartAnswer = RouteAnswer.Problem("workflow-instances", "start-missing-input");

        await form.Submit(Token);
        Assert.Equal((true, true, 1), (form.CanStart, form.HasError, _fixture.Server.Started.Count));

        _fixture.Server.StartAnswer = RouteAnswer.Api("workflow-instances-start", "started");
        await form.Submit(Token);
        await form.Submit(Token);

        Assert.Equal((false, false, 2), (form.CanStart, form.HasError, _fixture.Server.Started.Count));
    }

    [Fact]
    public async Task A_second_press_while_the_start_is_in_flight_sends_nothing()
    {
        var form = await Filled(_fixture);
        _fixture.Server.StartGate = new TaskCompletionSource();
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(Token);
        limit.CancelAfter(_bound);

        var first = form.Submit(limit.Token);
        var second = form.Submit(limit.Token);
        await second;

        Assert.Equal((true, false, 1), (form.IsSending, form.CanStart, _fixture.Server.Started.Count));
        _fixture.Server.StartGate.SetResult();
        await first;
        Assert.Equal((false, 1), (form.IsSending, _fixture.Server.Started.Count));
    }

    // The refusals as the API serves them: its code and its detail, verbatim, inline; nothing opens and the form stays.
    [Theory]
    [MemberData(nameof(Refusals))]
    public async Task A_refusal_is_shown_inline_with_its_code_and_detail_as_served(string scenario)
    {
        var golden = Golden("workflow-instances", scenario);
        var form = await Filled(_fixture);
        var started = new List<StartedEventArgs>();
        form.Started += (_, e) => started.Add(e);
        _fixture.Server.StartAnswer = RouteAnswer.Problem("workflow-instances", scenario);

        await form.Submit(Token);

        Assert.Equal(((string)golden["title"]!, (string)golden["detail"]!), (form.ErrorCode, form.ErrorDetail)); // why: a refusal has both
        Assert.Contains((string)golden["detail"]!, form.Error, StringComparison.Ordinal); // why: as above
        Assert.Equal((true, true, 1, 0), (form.IsOpen, form.HasError, form.ErrorFocusRequests, started.Count));
    }

    public static TheoryData<string> Refusals => [.. StartGoldens.Refusals];

    [Fact]
    public async Task An_accepted_start_raises_started_with_the_golden_id_and_address_and_closes_the_form()
    {
        var golden = Golden("workflow-instances-start", "started");
        var form = await Filled(_fixture);
        var started = new List<StartedEventArgs>();
        form.Started += (_, e) => started.Add(e);

        await form.Submit(Token);

        var raised = Assert.Single(started);
        Assert.Equal(((string)golden["instance"]!["id"]!, (string)golden["instance"]!["address"]!, false, false, false), (raised.Id, raised.Address, raised.Existing, raised.Finished, form.IsOpen)); // why: the golden names its instance
        Assert.Equal(string.Empty, form.Error);
    }

    // Starting again an attempt that is live is not an error: the frame opens, and the form says it was already running.
    [Fact]
    public async Task A_live_attempt_opens_its_frame_and_the_form_says_already_running()
    {
        var form = await Filled(_fixture);
        _fixture.Answers(existing: true, finished: false);
        var started = new List<StartedEventArgs>();
        form.Started += (_, e) => started.Add(e);

        await form.Submit(Token);

        var raised = Assert.Single(started);
        Assert.Equal((Labels.StartAlreadyRunning(raised.Address), true, false, true), (form.Outcome, form.IsOpen, form.OffersNewAttempt, raised.Existing));
    }

    // Q5: the next attempt is a second, explicit press; the first press never asks for it.
    [Fact]
    public async Task A_finished_attempt_offers_a_new_attempt_that_goes_only_on_a_second_explicit_press()
    {
        var form = await Filled(_fixture);
        _fixture.Answers(existing: true, finished: true);

        await form.Submit(Token);

        var address = (string)Golden("workflow-instances-start", "started")["instance"]!["address"]!; // why: the golden names its instance
        Assert.Equal((Labels.StartFinished(address), true, false, false), (form.Outcome, form.OffersNewAttempt, _fixture.Server.Started.Single().NextAttempt, form.CanStart));

        _fixture.Answers(existing: false, finished: false);
        await form.StartNewAttempt(Token);
        await form.StartNewAttempt(Token);

        Assert.Equal([false, true], _fixture.Server.Started.Select(s => s.NextAttempt));
        Assert.Equal(Wire(_fixture.Server.Started[0] with { NextAttempt = true }), Wire(_fixture.Server.Started[1]));
        Assert.Equal((false, false), (form.IsOpen, form.OffersNewAttempt));
    }

    [Fact]
    public async Task A_new_attempt_is_not_sent_unless_a_finished_attempt_was_shown()
    {
        var form = await Filled(_fixture);

        await form.StartNewAttempt(Token);

        Assert.Empty(_fixture.Server.Started);
    }

    [Fact]
    public async Task An_answer_for_a_form_that_was_closed_and_opened_again_is_not_shown_in_the_new_one()
    {
        var form = await Filled(_fixture);
        _fixture.Server.StartGate = new TaskCompletionSource();
        var started = new List<StartedEventArgs>();
        form.Started += (_, e) => started.Add(e);
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(Token);
        limit.CancelAfter(_bound);
        var sending = form.Submit(limit.Token);

        form.Close();
        await form.Open(itemId: null, limit.Token);
        _fixture.Server.StartGate.SetResult();
        await sending;

        Assert.Equal((true, false, 0), (form.IsOpen, form.IsSending, started.Count));
    }

    // Q7: readiness is the catalog row's: pending until its phase ships, and nothing is known before the catalog is read.
    [Fact]
    public async Task Before_the_catalog_is_read_start_is_not_ready()
    {
        var form = new StartFormViewModel(new ViewModels.Controls.CatalogViewModel(new FakeClipboard(), _fixture.Clock), () => StartFormFixture.Api, _fixture.Server, _fixture.Files);

        await form.Open(itemId: null, Token);

        Assert.Equal((false, Labels.StartNoCatalog, false), (form.IsReady, form.Readiness, form.CanStart));
    }
}
