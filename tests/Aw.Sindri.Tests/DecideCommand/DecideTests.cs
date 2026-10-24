using Aw.Kernel.Ids;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.Requests;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.Decide;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Views;

namespace Aw.Sindri.Tests.DecideCommand;

/// <summary>Deciding (API spec §3.2, P4 plan Unit E): one 'decision.recorded' by the caller, keyed by the decision, so a second decide of it is 'decided elsewhere'.</summary>
[Lane(TestLane.Unit)]
public sealed class DecideTests
{
    private static readonly DecisionOption _accept = DecisionOption.From("accept");
    private static readonly DecisionOption _decline = DecisionOption.From("decline");
    private readonly ViewsHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private DecideService Service(ITextScrubber? scrubber = null) =>
        new(_h.Views, _h.Slice.Store, _h.Slice.Held, _h.Slice.Events, scrubber ?? new PassThroughScrubber(), _h.Slice.Callers);

    private IDisposable Human() => _h.Slice.Callers.Enter(A.Caller().Human().Build());

    /// <summary>A pending decision (the first one), about no item unless <paramref name="item"/> is given.</summary>
    private async Task Pending(ItemId? item = null, string? repo = null)
    {
        _ = await _h.Slice.HoldLease();
        if (item is { } named)
        {
            await _h.Fold(EventTypes.ItemCreated, new ItemCreated(_h.Slice.Ids.New<CommandId>(), named, "Ship it", repo is null ? null : RepoName.From(repo), Link: null, Body: null, Owner: null), named);
        }

        var filed = new RequestFiled(_h.Slice.Ids.New<CommandId>(), A.FirstRequestId, A.FirstDecisionId, RequestKind.Rescope, item, repo is null ? null : RepoName.From(repo), "Split it.");
        if (item is { } about)
        {
            await _h.Fold(EventTypes.RequestFiled, filed, about);
        }
        else
        {
            await _h.Fold(EventTypes.RequestFiled, filed);
        }
    }

    [Fact]
    public async Task Decide_appends_decision_recorded_by_caller()
    {
        await Pending(A.FirstItemId, "web");
        using var entered = Human();

        var result = await Service().Handle(A.DecideRequest().Choosing(_accept).WithNote("Looks right.").Build(), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        var recorded = Assert.Single(_h.Slice.Store.Events);
        Expect.Equal(EventTypes.DecisionRecorded, recorded.Envelope.Type);
        Expect.Equal(recorded.Sequence, result.Value.Sequence);
        Expect.Equal($"decision.recorded:{A.FirstDecisionId.Value}", recorded.IdempotencyKey);
        Expect.Equal(A.FirstItemId.Value, recorded.Item);
        var payload = _h.Slice.Events.Payload<DecisionRecorded>(recorded).Value;
        Expect.Equal((A.FirstDecisionId, _accept, "Looks right.", "human:http"), (payload.Decision, payload.Choice, payload.Note, payload.By));
        Expect.Equal(recorded.Envelope.Actor, payload.By);
        Expect.Equal(payload.CommandId, result.Value.CommandId);
    }

    [Fact]
    public async Task Decline_is_an_option_and_a_decision_about_no_item_has_no_item_on_the_event()
    {
        await Pending();
        using var entered = Human();

        var result = await Service().Handle(A.DecideRequest().Choosing(_decline).Build(), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        var recorded = Assert.Single(_h.Slice.Store.Events);
        Assert.Null(recorded.Item);
        var payload = _h.Slice.Events.Payload<DecisionRecorded>(recorded).Value;
        Expect.Equal(_decline, payload.Choice);
        Assert.Null(payload.Note);
    }

    [Fact]
    public async Task A_choice_outside_the_options_is_SND_DEC_001_and_records_nothing()
    {
        await Pending();
        using var entered = Human();

        var result = await Service().Handle(A.DecideRequest().Choosing(DecisionOption.From("maybe")).Build(), Token);

        Expect.Equal(SindriErrors.DecisionInvalid.Code, result.Error.Code);
        Assert.Empty(_h.Slice.Store.Events);
    }

    [Fact]
    public async Task A_note_over_65536_characters_is_SND_DEC_001()
    {
        await Pending();
        using var entered = Human();

        var result = await Service().Handle(A.DecideRequest().WithNote(new string('n', DecisionRules.NoteMax + 1)).Build(), Token);

        Expect.Equal(SindriErrors.DecisionInvalid.Code, result.Error.Code);
        Assert.Empty(_h.Slice.Store.Events);
    }

    [Fact]
    public async Task The_note_is_scrubbed_in_the_ledger()
    {
        await Pending();
        using var entered = Human();

        var result = await Service(new MaskScrubber("hunter2")).Handle(A.DecideRequest().WithNote("the password is hunter2").Build(), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        var recorded = Assert.Single(_h.Slice.Store.Events);
        Assert.DoesNotContain("hunter2", recorded.Envelope.Payload, StringComparison.Ordinal);
        Expect.Equal("the password is [masked]", _h.Slice.Events.Payload<DecisionRecorded>(recorded).Value.Note);
    }

    [Fact]
    public async Task An_unknown_decision_is_SND_DEC_404()
    {
        await Pending();
        using var entered = Human();

        var result = await Service().Handle(A.DecideRequest().ForDecision(DecisionId.From("DEC-" + new string('9', 32))).Build(), Token);

        Expect.Equal(SindriErrors.DecisionNotFound.Code, result.Error.Code);
        Assert.Empty(_h.Slice.Store.Events);
    }

    [Fact]
    public async Task Deciding_a_decided_decision_is_SND_ITEM_409_decided_elsewhere()
    {
        await Pending();
        await _h.Fold(EventTypes.DecisionRecorded, new DecisionRecorded(_h.Slice.Ids.New<CommandId>(), A.FirstDecisionId, _accept, Note: null, "human:ui"));
        using var entered = Human();

        var result = await Service().Handle(A.DecideRequest().Build(), Token);

        Expect.Equal(SindriErrors.ItemConflict.Code, result.Error.Code);
        Assert.Contains("decided elsewhere", result.Error.Message, StringComparison.Ordinal);
        Assert.Empty(_h.Slice.Store.Events);
    }

    [Fact]
    public async Task Decide_on_voided_is_409_item_closed()
    {
        await Pending(A.FirstItemId, "web");
        await _h.Fold(EventTypes.ItemStateChanged, new ItemStateChanged(_h.Slice.Ids.New<CommandId>(), A.FirstItemId, ItemState.NeedsDecision, ItemState.Cancelled, 2, Reason: null), A.FirstItemId);
        using var entered = Human();

        var result = await Service().Handle(A.DecideRequest().Build(), Token);

        Expect.Equal(SindriErrors.ItemConflict.Code, result.Error.Code);
        Assert.Contains("the item was closed", result.Error.Message, StringComparison.Ordinal);
        Assert.Empty(_h.Slice.Store.Events);
    }

    [Fact]
    public async Task Losing_the_key_race_is_SND_ITEM_409_decided_elsewhere()
    {
        await Pending();
        var rival = new DecisionRecorded(_h.Slice.Ids.New<CommandId>(), A.FirstDecisionId, _decline, Note: null, "human:ui");
        _ = await _h.Slice.Store.Append(_h.Slice.Events.Entry(EventTypes.DecisionRecorded, rival, $"decision.recorded:{A.FirstDecisionId.Value}"), _h.Slice.Held.Current!, Token);
        using var entered = Human();

        var result = await Service().Handle(A.DecideRequest().Build(), Token);

        Expect.Equal(SindriErrors.ItemConflict.Code, result.Error.Code);
        Assert.Contains("decided elsewhere", result.Error.Message, StringComparison.Ordinal);
        _ = Assert.Single(_h.Slice.Store.Events);
    }

    [Fact]
    public async Task Two_concurrent_decides_one_event_one_409()
    {
        await Pending();
        using var entered = Human();
        using var barrier = new Barrier(2);
        var service = Service();

        var both = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Task.Run(async () =>
        {
            barrier.SignalAndWait(Token);
            return await service.Handle(A.DecideRequest().Build(), Token);
        }, Token)));

        Expect.Equal(1, both.Count(r => r.IsSuccess));
        Expect.Equal(SindriErrors.ItemConflict.Code, Assert.Single(both, r => r.IsError).Error.Code);
        _ = Assert.Single(_h.Slice.Store.Events);
    }

    [Fact]
    public async Task Without_a_lease_it_is_not_running()
    {
        using var entered = Human();

        var result = await Service().Handle(A.DecideRequest().Build(), Token);

        Expect.Equal(SindriErrors.NotRunning.Code, result.Error.Code);
    }

    [Fact]
    public async Task Without_a_caller_it_is_refused()
    {
        await Pending();

        var result = await Service().Handle(A.DecideRequest().Build(), Token);

        Expect.Equal(SindriErrors.AuthRefused.Code, result.Error.Code);
    }

    private sealed class MaskScrubber(string secret) : ITextScrubber
    {
        public string Scrub(string text) => text.Replace(secret, "[masked]", StringComparison.Ordinal);
    }
}
