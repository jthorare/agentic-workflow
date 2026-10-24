using Aw.Kernel.Ids;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Decisions;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.Requests;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Views;

/// <summary>The 'requests' and 'decisions' folds and the item state they derive (P4 plan Unit C, G3, G5).</summary>
public sealed class RequestsProjectionTests
{
    private static readonly RepoName _repo = RepoName.From("agentic-workflow");
    private static readonly DecisionOption _accept = DecisionOption.From("accept");
    private readonly ViewsHarness _v = new();
    private readonly ItemId _item = A.FirstItemId;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private SliceHarness H => _v.Slice;

    [Fact, Trait("Lane", "unit")]
    public async Task Request_filed_derives_one_pending_accept_request_decision()
    {
        await CreateItem();
        await File(A.FirstRequestId, A.FirstDecisionId, RequestKind.Rescope, "Split the work.", _item);

        var request = (await _v.Views.Get<RequestRow>(ViewNames.Requests, A.FirstRequestId.Value, Token)).Value!;
        var decision = (await _v.Views.Get<HumanDecisionRow>(ViewNames.Decisions, A.FirstDecisionId.Value, Token)).Value!;

        Assert.Equal((A.FirstRequestId, RequestKind.Rescope, _item, _repo, "Split the work.", RequestState.Open, A.FirstDecisionId), (request.Id, request.Kind, request.Item, request.Repo, request.Text, request.State, request.Decision));
        Assert.Equal((A.FirstDecisionId, A.FirstRequestId, DecisionKind.AcceptRequest, RequestKind.Rescope, _item, 1L, _repo), (decision.Id, decision.Request, decision.Kind, decision.RequestKind, decision.Item, decision.ItemVersion, decision.Repo));
        Assert.Equal((DecisionState.Pending, "Split the work."), (decision.State, decision.Text));
        Assert.Equal((request.Actor, request.FiledAt), (decision.FiledBy, decision.FiledAt));
        Assert.Null(decision.DecidedBy);
        Assert.Null(decision.Choice);
        Assert.Null(decision.Cleared);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_request_without_an_item_has_item_version_0()
    {
        await File(A.FirstRequestId, A.FirstDecisionId, RequestKind.Note, "FYI", item: null);

        var decision = (await _v.Views.Get<HumanDecisionRow>(ViewNames.Decisions, A.FirstDecisionId.Value, Token)).Value!;

        Assert.Equal((0L, null, RequestKind.Note), (decision.ItemVersion, decision.Item, decision.RequestKind));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Decision_recorded_accepts_the_request()
    {
        await CreateItem();
        await File(A.FirstRequestId, A.FirstDecisionId, RequestKind.Rescope, "Split the work.", _item);
        H.Clock.Advance(TimeSpan.FromMinutes(3));

        await Decide(A.FirstDecisionId, _accept, "go ahead", "human:ui");

        var decision = (await _v.Views.Get<HumanDecisionRow>(ViewNames.Decisions, A.FirstDecisionId.Value, Token)).Value!;
        var request = (await _v.Views.Get<RequestRow>(ViewNames.Requests, A.FirstRequestId.Value, Token)).Value!;
        Assert.Equal((DecisionState.Decided, _accept, "human:ui", "go ahead"), (decision.State, decision.Choice, decision.DecidedBy, decision.Note));
        Assert.Equal(H.Clock.GetUtcNow(), decision.DecidedAt);
        Assert.Equal("decision " + A.FirstDecisionId.Value + " accepted by human:ui", decision.Cleared);
        Assert.Equal(RequestState.Accepted, request.State);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Declining_declines_the_request()
    {
        await File(A.FirstRequestId, A.FirstDecisionId, RequestKind.AddTask, "Add a test.", item: null);

        await Decide(A.FirstDecisionId, DecisionOption.From("decline"), note: null, "human:prover");

        var decision = (await _v.Views.Get<HumanDecisionRow>(ViewNames.Decisions, A.FirstDecisionId.Value, Token)).Value!;
        Assert.Equal(RequestState.Declined, (await _v.Views.Get<RequestRow>(ViewNames.Requests, A.FirstRequestId.Value, Token)).Value!.State);
        Assert.Equal("decision " + A.FirstDecisionId.Value + " declined by human:prover", decision.Cleared);
        Assert.Null(decision.Note);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Close_voids_pending_decisions_and_open_requests()
    {
        var other = ItemId.From("ITM-00000000000000000000000000000002");
        var (second, secondDecision) = (RequestId.From("REQ-00000000000000000000000000000002"), DecisionId.From("DEC-00000000000000000000000000000002"));
        var (third, thirdDecision) = (RequestId.From("REQ-00000000000000000000000000000003"), DecisionId.From("DEC-00000000000000000000000000000003"));
        await CreateItem();
        await CreateItem(other);
        await File(A.FirstRequestId, A.FirstDecisionId, RequestKind.Rescope, "one", _item);
        await File(second, secondDecision, RequestKind.Note, "two", _item);
        await File(third, thirdDecision, RequestKind.Note, "elsewhere", other);
        await Decide(secondDecision, _accept, note: null, "human:ui");
        H.Clock.Advance(TimeSpan.FromMinutes(1));

        await Close(_item, ItemState.Done, version: 2);

        var voided = (await _v.Views.Get<HumanDecisionRow>(ViewNames.Decisions, A.FirstDecisionId.Value, Token)).Value!;
        Assert.Equal((DecisionState.Voided, H.Clock.GetUtcNow(), EmptyText.Voided()), (voided.State, voided.DecidedAt, voided.Cleared));
        Assert.Null(voided.Choice);
        Assert.Equal(RequestState.Voided, (await _v.Views.Get<RequestRow>(ViewNames.Requests, A.FirstRequestId.Value, Token)).Value!.State);
        Assert.Equal(DecisionState.Decided, (await _v.Views.Get<HumanDecisionRow>(ViewNames.Decisions, secondDecision.Value, Token)).Value!.State);   // already settled: left alone
        Assert.Equal(RequestState.Accepted, (await _v.Views.Get<RequestRow>(ViewNames.Requests, second.Value, Token)).Value!.State);
        Assert.Equal(DecisionState.Pending, (await _v.Views.Get<HumanDecisionRow>(ViewNames.Decisions, thirdDecision.Value, Token)).Value!.State);   // another item's
        Assert.Equal(RequestState.Open, (await _v.Views.Get<RequestRow>(ViewNames.Requests, third.Value, Token)).Value!.State);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cancelling_voids_too()
    {
        await CreateItem();
        await File(A.FirstRequestId, A.FirstDecisionId, RequestKind.Rescope, "one", _item);

        await Close(_item, ItemState.Cancelled, version: 2);

        Assert.Equal(DecisionState.Voided, (await _v.Views.Get<HumanDecisionRow>(ViewNames.Decisions, A.FirstDecisionId.Value, Token)).Value!.State);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Item_with_pending_decision_reads_needs_decision()
    {
        var (second, secondDecision) = (RequestId.From("REQ-00000000000000000000000000000002"), DecisionId.From("DEC-00000000000000000000000000000002"));
        await CreateItem();
        Assert.Equal(ItemState.New, (await Item()).State);

        await File(A.FirstRequestId, A.FirstDecisionId, RequestKind.Rescope, "one", _item);
        var waiting = await Item();
        Assert.Equal((ItemState.NeedsDecision, 1L, A.FirstDecisionId.Value), (waiting.State, waiting.Version, waiting.WaitingOn));

        await File(second, secondDecision, RequestKind.Note, "two", _item);
        Assert.Equal((ItemState.NeedsDecision, A.FirstDecisionId.Value), ((await Item()).State, (await Item()).WaitingOn));   // still waits on the oldest

        await Decide(A.FirstDecisionId, _accept, note: null, "human:ui");
        var rest = await Item();
        Assert.Equal((ItemState.NeedsDecision, secondDecision.Value), (rest.State, rest.WaitingOn));

        await Decide(secondDecision, _accept, note: null, "human:ui");
        var done = await Item();
        Assert.Equal((ItemState.New, null, 1L), (done.State, done.WaitingOn, done.Version));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Closing_an_item_that_needs_a_decision_ends_in_the_closed_state()
    {
        await CreateItem();
        await File(A.FirstRequestId, A.FirstDecisionId, RequestKind.Rescope, "one", _item);

        await Close(_item, ItemState.Done, version: 2, from: ItemState.NeedsDecision);

        var row = await Item();
        Assert.Equal((ItemState.Done, 2L, null), (row.State, row.Version, row.WaitingOn));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_decision_recorded_after_its_item_closed_leaves_the_voided_rows_as_they_are()
    {
        await CreateItem();
        await File(A.FirstRequestId, A.FirstDecisionId, RequestKind.Rescope, "one", _item);
        await Close(_item, ItemState.Done, version: 2, from: ItemState.NeedsDecision);
        var voided = (await _v.Views.Get<HumanDecisionRow>(ViewNames.Decisions, A.FirstDecisionId.Value, Token)).Value!;

        await Decide(A.FirstDecisionId, _accept, note: "late", "human:ui");

        Assert.Equal(voided, (await _v.Views.Get<HumanDecisionRow>(ViewNames.Decisions, A.FirstDecisionId.Value, Token)).Value);
        Assert.Equal(RequestState.Voided, (await _v.Views.Get<RequestRow>(ViewNames.Requests, A.FirstRequestId.Value, Token)).Value!.State);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_request_does_not_move_a_running_item()
    {
        await CreateItem();
        await Close(_item, ItemState.Running, version: 2);   // the engine's later states: only 'new' reads needs-decision (G5)

        await File(A.FirstRequestId, A.FirstDecisionId, RequestKind.Rescope, "one", _item);

        Assert.Equal(ItemState.Running, (await Item()).State);
    }

    [Fact, Trait("Lane", "unit")]
    public void Request_and_decision_text_is_scrubbed_escaped_and_cut()
    {
        var scrubber = new MaskScrubber("SECRET");
        var filed = new RequestFiled(H.Ids.New<CommandId>(), A.FirstRequestId, A.FirstDecisionId, RequestKind.Rescope, Item: null, Repo: null, "Fix SECRET\u001b[31m\nnow " + new string('x', RequestRules.TextMax + 1000));
        var recorded = Recorded.Of(H.Events, EventTypes.RequestFiled, filed, 1);

        var rows = RequestsProjection.Fold(filed, recorded, scrubber, itemVersion: 0);

        Assert.StartsWith("Fix [masked]\\x1B[31m now xxx", rows.Request.Text, StringComparison.Ordinal);
        Assert.Equal(RequestRules.TextMax, rows.Request.Text.Length);
        Assert.Equal(rows.Request.Text, rows.Decision.Text);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_note_is_scrubbed_and_escaped()
    {
        var filed = new RequestFiled(H.Ids.New<CommandId>(), A.FirstRequestId, A.FirstDecisionId, RequestKind.Note, Item: null, Repo: null, "hi");
        var rows = RequestsProjection.Fold(filed, Recorded.Of(H.Events, EventTypes.RequestFiled, filed, 1), new PassThroughScrubber(), itemVersion: 0);
        var payload = new DecisionRecorded(H.Ids.New<CommandId>(), A.FirstDecisionId, _accept, "ok SECRET\u001b[0m", "human:ui");

        var decided = RequestsProjection.Fold(payload, rows.Decision, rows.Request, Recorded.Of(H.Events, EventTypes.DecisionRecorded, payload, 2), new MaskScrubber("SECRET")).Value;

        Assert.Equal("ok [masked]\\x1B[0m", decided.Decision.Note);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_choice_that_is_not_accept_or_decline_stalls_the_fold()
    {
        var filed = new RequestFiled(H.Ids.New<CommandId>(), A.FirstRequestId, A.FirstDecisionId, RequestKind.Note, Item: null, Repo: null, "hi");
        var rows = RequestsProjection.Fold(filed, Recorded.Of(H.Events, EventTypes.RequestFiled, filed, 1), new PassThroughScrubber(), itemVersion: 0);
        var payload = new DecisionRecorded(H.Ids.New<CommandId>(), A.FirstDecisionId, DecisionOption.From("maybe"), Note: null, "human:ui");

        var decided = RequestsProjection.Fold(payload, rows.Decision, rows.Request, Recorded.Of(H.Events, EventTypes.DecisionRecorded, payload, 2), new PassThroughScrubber());

        Assert.Equal(SindriErrors.ViewsStalled.Code, decided.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_decision_for_a_decision_that_was_never_filed_stalls_the_projector()
    {
        var ct = Token;
        _ = (await _v.Views.Open(ViewSchema.Version, ct)).Value;
        var payload = new DecisionRecorded(H.Ids.New<CommandId>(), A.FirstDecisionId, _accept, Note: null, "human:ui");

        var applied = (await _v.Views.Apply([Recorded.Of(H.Events, EventTypes.DecisionRecorded, payload, 1)], [new RequestsProjection(H.Events, new PassThroughScrubber())], H.Clock.GetUtcNow(), ct)).Value;

        Assert.Equal(SindriErrors.ViewsStalled.Code, applied.Stall?.Code);
    }

    private async Task<ItemRow> Item() => (await _v.Views.Get<ItemRow>(ViewNames.Items, _item.Value, Token)).Value!;

    private async Task CreateItem(ItemId? id = null)
    {
        var item = id ?? _item;
        await _v.Fold(EventTypes.ItemCreated, new ItemCreated(H.Ids.New<CommandId>(), item, "Try the queue", _repo, Link: null, Body: null, Owner: null), item);
    }

    private Task File(RequestId request, DecisionId decision, RequestKind kind, string text, ItemId? item) =>
        item is { } named
            ? _v.Fold(EventTypes.RequestFiled, new RequestFiled(H.Ids.New<CommandId>(), request, decision, kind, named, _repo, text), named)
            : _v.Fold(EventTypes.RequestFiled, new RequestFiled(H.Ids.New<CommandId>(), request, decision, kind, Item: null, _repo, text));

    private Task Decide(DecisionId decision, DecisionOption choice, string? note, string by) =>
        _v.Fold(EventTypes.DecisionRecorded, new DecisionRecorded(H.Ids.New<CommandId>(), decision, choice, note, by));

    private Task Close(ItemId item, ItemState to, long version, ItemState from = ItemState.New) =>
        _v.Fold(EventTypes.ItemStateChanged, new ItemStateChanged(H.Ids.New<CommandId>(), item, from, to, version, Reason: null), item);

    private sealed class MaskScrubber(string secret) : ITextScrubber
    {
        public string Scrub(string text) => text.Replace(secret, "[masked]", StringComparison.Ordinal);
    }
}
