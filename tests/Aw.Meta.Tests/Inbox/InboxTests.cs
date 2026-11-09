using Aw.Meta.Core;
using Aw.Meta.Core.Ids;
using Aw.Meta.Features.Inbox;
using Aw.Meta.Features.InboxDefer;
using Aw.Meta.Features.InboxDismiss;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Inbox;

public sealed class InboxTests
{
    private readonly MetaWorld _world = new();

    private ListInboxService List() => new(_world.Config, _world.Messages, _world.Reader, _world.Clock);

    private DismissInboxService Dismiss() => new(_world.Messages);

    private DeferInboxService Defer() => new(_world.Messages, _world.Clock);

    private async Task Receive(params (string Id, string Name, string Body)[] messages)
    {
        _world.Transcripts.Write(MetaWorld.Transcript, string.Concat(messages.Select(m => MetaWorld.PeerLine(m.Id, m.Name, m.Body))));
        _ = await _world.Sync.Run(MetaWorld.Transcript, CancellationToken.None);
    }

    private async Task<IReadOnlyList<InboxItem>> Items(bool all)
    {
        var result = await List().Handle(new ListInboxRequest(all), CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        return result.Value.Items;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_default_list_holds_only_messages_still_owed_a_reply()
    {
        await _world.Ready();
        await Receive(("m1", "t1-api", "please rebase"), ("m2", "t1-ui", "ready #190 079c2541"));
        _ = await _world.Messages.RecordOutbound(A.Sent("t1-api", "done", 61), CancellationToken.None);

        var open = await Items(all: false);

        var only = Assert.Single(open);
        Assert.Equal(("m2", "t1-ui", "Ready", "open"), (only.Id.Value, only.From, only.Kind, only.State));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task All_lists_every_message_with_its_state()
    {
        await _world.Ready();
        await Receive(("m1", "t1-api", "a"), ("m2", "t1-ui", "b"), ("m3", "t1-api", "c"), ("m4", "t1-ui", "d"));
        _ = await _world.Messages.Resolve(MessageId.From("m2"), new(Core.Messages.MessageResolutionKind.Dismissed, "fyi"), CancellationToken.None);
        var until = _world.Clock.GetUtcNow().AddHours(1);
        _ = await _world.Messages.Resolve(MessageId.From("m3"), new(Core.Messages.MessageResolutionKind.Deferred, "later", until), CancellationToken.None);
        _ = await _world.Messages.RecordOutbound(A.Sent("t1-api", "ok", 61), CancellationToken.None);

        var all = await Items(all: true);

        // m1 and m3 are both from t1-api, so the send answers m1; m3 is deferred, which still shows as deferred.
        Assert.Equal(["answered", "dismissed", "deferred", "open"], all.Select(i => i.State), StringComparer.Ordinal);
        Assert.Equal(until, all[2].Until);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_a_team_config_it_fails_with_AW_META_002()
    {
        var result = await List().Handle(new ListInboxRequest(All: false), CancellationToken.None);

        Assert.Equal(MetaErrors.NoTeamConfig.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_dismissed_message_stops_being_owed()
    {
        await _world.Ready();
        await Receive(("m1", "t1-api", "fyi only"));

        var result = await Dismiss().Handle(new DismissInboxRequest(MessageId.From("m1"), "no reply needed"), CancellationToken.None);

        Assert.Equal("m1", result.Value.Id.Value);
        Assert.Empty(await Items(all: false));
        Assert.Empty(await _world.Open());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Dismissing_an_unknown_message_is_AW_META_005()
    {
        await _world.Ready();

        var result = await Dismiss().Handle(new DismissInboxRequest(MessageId.From("nope"), "x"), CancellationToken.None);

        Assert.Equal(MetaErrors.MessageNotFound.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_dismissal_needs_a_reason() =>
        Assert.True(new DismissInboxValidator().Validate(new DismissInboxRequest(MessageId.From("m1"), "  ")).IsError);

    [Fact, Trait("Lane", "unit")]
    public async Task A_deferred_message_reopens_when_it_is_due()
    {
        await _world.Ready();
        await Receive(("m1", "t1-api", "later please"));

        var deferred = await Defer().Handle(new DeferInboxRequest(MessageId.From("m1"), "after the merge", "+30m"), CancellationToken.None);
        var snoozed = await _world.Open();
        _world.Clock.Advance(TimeSpan.FromMinutes(31));
        var due = await _world.Open();

        Assert.Equal(_world.Clock.GetUtcNow().AddMinutes(-1), deferred.Value.Until);
        Assert.Empty(snoozed);
        Assert.Equal(["m1", "watch"], due.Select(o => o.Key), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_ISO_time_sets_the_due_time_exactly()
    {
        await _world.Ready();
        await Receive(("m1", "t1-api", "x"));

        var deferred = await Defer().Handle(new DeferInboxRequest(MessageId.From("m1"), "r", "2026-10-16T09:00:00Z"), CancellationToken.None);

        Assert.Equal(new DateTimeOffset(2026, 10, 16, 9, 0, 0, TimeSpan.Zero), deferred.Value.Until);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("soon", "why")]
    [InlineData("+30m", "")]
    public void A_deferral_needs_a_reason_and_a_time_that_can_be_read(string until, string reason) =>
        Assert.True(new DeferInboxValidator().Validate(new DeferInboxRequest(MessageId.From("m1"), reason, until)).IsError);
}
