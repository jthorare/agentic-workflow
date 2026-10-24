using Aw.Kernel.Ids;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Features.Items;
using Aw.Sindri.Features.RecordHeartbeat;
using Aw.Sindri.Features.StreamEvents;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Views;

namespace Aw.Sindri.Tests.StreamEvents;

public sealed class StreamEventsTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task StreamEvents_returns_the_events_after_the_cursor_with_their_envelope()
    {
        var h = new SliceHarness();
        var grant = await h.HoldLease();
        var record = new RecordHeartbeatService(h.Store, h.Events, h.Clock);
        for (var i = 0; i < 3; i++)
        {
            _ = await record.Handle(new RecordHeartbeatRequest(new ScheduledRun(grant, h.Clock.GetUtcNow().AddSeconds(i))), TestContext.Current.CancellationToken);
        }

        var page = (await StreamEventsServices.Over(h).Handle(new StreamEventsRequest(After: 1), TestContext.Current.CancellationToken)).Value;

        Assert.Equal([2L, 3L], page.Events.Select(e => e.Sequence));
        Assert.All(page.Events, e => Assert.Equal(("service.heartbeat", "local"), (e.Type, e.Tenant)));
        Assert.Equal(3L, page.ScannedTo);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_item_created_row_comes_in_its_public_form_with_a_clean_title()
    {
        var h = new SliceHarness();
        var grant = await h.HoldLease();
        var created = new ItemCreated(h.Ids.New<CommandId>(), A.FirstItemId, "Fix \u001b[31mSECRET\u001b[0m", Repo: null, Link: null, Body: "private SECRET body", Owner: null);
        _ = await h.Store.Append(h.Events.Entry(EventTypes.ItemCreated, created, "item.created:x", A.FirstItemId.Value), grant, TestContext.Current.CancellationToken);
        var service = StreamEventsServices.Over(h, new MaskScrubber("SECRET"));

        var row = Assert.Single((await service.Handle(new StreamEventsRequest(After: 0), TestContext.Current.CancellationToken)).Value.Events);

        Assert.False(row.Payload.TryGetProperty("body", out _));
        Assert.Equal("Fix \\x1B[31m[masked]\\x1B[0m", row.Payload.GetProperty("title").GetString());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_reads_every_repos_items_with_the_views_empty()
    {
        var h = new SliceHarness();
        var grant = await h.HoldLease();
        var ct = TestContext.Current.CancellationToken;
        var (mine, other) = (A.FirstItemId, ItemId.From("ITM-00000000000000000000000000000002"));
        foreach (var (item, repo) in new[] { (mine, "demo"), (other, "elsewhere") })
        {
            var created = new ItemCreated(h.Ids.New<CommandId>(), item, "t", RepoName.From(repo), Link: null, Body: null, Owner: null);
            _ = await h.Store.Append(h.Events.Entry(EventTypes.ItemCreated, created, "item.created:" + item.Value, item.Value), grant, ct);
            var moved = new ItemStateChanged(h.Ids.New<CommandId>(), item, ItemState.New, ItemState.Queued, 2, Reason: null);
            _ = await h.Store.Append(h.Events.Entry(EventTypes.ItemStateChanged, moved, "item.state-changed:" + item.Value + ":v2", item.Value), grant, ct);
        }

        // the items view holds nothing, as during a rebuild: the rows come from the ledger
        var session = new CallerContext(CallerClass.Session, Terminal: false, "session:cli", h.Ids.New<CommandId>(), h.Ids.New<CorrelationId>(), RepoName.From("demo"));
        using var entered = h.Callers.Enter(session);
        var page = (await StreamEventsServices.Over(h).Handle(new StreamEventsRequest(After: 0), ct)).Value;

        Assert.Equal([1L, 2L, 3L, 4L], page.Events.Select(e => e.Sequence));
        Assert.Equal([mine, mine, other, other], page.Events.Select(e => e.Item));
        Assert.Equal(4L, page.ScannedTo);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Only_public_types_are_streamed()
    {
        var h = new SliceHarness();
        var grant = await h.HoldLease();
        var ct = TestContext.Current.CancellationToken;
        var handled = new CommandHandled(h.Ids.New<CommandId>(), ActionId.From("CreateItem"), ActionClass.Desk, "human:cli", CommandOutcome.Accepted, Code: null);
        _ = await h.Store.Append(h.Events.Entry(EventTypes.CommandHandled, handled, "command.handled:1"), grant, ct);
        _ = await h.Store.Append(h.Events.Entry("service.heartbeat", new Heartbeat(h.Clock.GetUtcNow(), h.Clock.GetUtcNow()), "service.heartbeat:1:1"), grant, ct);

        var page = (await StreamEventsServices.Over(h).Handle(new StreamEventsRequest(After: 0), ct)).Value;

        Assert.Equal([2L], page.Events.Select(e => e.Sequence));
        Assert.Equal(2L, page.ScannedTo);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Types_filter_keeps_only_the_matching_public_types()
    {
        var h = new SliceHarness();
        var grant = await h.HoldLease();
        var ct = TestContext.Current.CancellationToken;
        await Append(h, grant, EventTypes.ProcessStopped, new ProcessStopped(1, 1, StopReason.Stop), "process.stopped:1");
        await Append(h, grant, "service.heartbeat", new Heartbeat(h.Clock.GetUtcNow(), h.Clock.GetUtcNow()), "service.heartbeat:1:1");
        await Append(h, grant, EventTypes.ProcessStopped, new ProcessStopped(2, 2, StopReason.Stop), "process.stopped:2");

        var page = (await StreamEventsServices.Over(h).Handle(new StreamEventsRequest(After: 0, Types: TypeFilter.Parse("service.*").Value), ct)).Value;

        Assert.Equal([2L], page.Events.Select(e => e.Sequence));
        Assert.Equal(3L, page.ScannedTo);   // the cursor moves past the rows the filter skipped
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Item_filter_by_id_and_by_alias()
    {
        var views = new ViewsHarness();
        var h = views.Slice;
        var grant = await h.HoldLease();
        var ct = TestContext.Current.CancellationToken;
        var (mine, other) = (A.FirstItemId, ItemId.From("ITM-00000000000000000000000000000002"));
        var alias = ItemAlias.From("ENG-142");
        foreach (var item in new[] { mine, other })
        {
            var created = new ItemCreated(h.Ids.New<CommandId>(), item, "t", Repo: null, Link: null, Body: null, Owner: null);
            await Append(h, grant, EventTypes.ItemCreated, created, "item.created:" + item.Value, item.Value);
            await views.Fold(EventTypes.ItemCreated, created, item);
        }

        await views.Fold(EventTypes.ItemLinked, new ItemLinked(h.Ids.New<CommandId>(), mine, RecordKey.From("linear:issue/abc-123"), alias), mine);
        using var entered = h.Callers.Enter(new CallerContextBuilder().Human().Build());
        var service = StreamEventsServices.Over(h, new PassThroughScrubber(), new ViewItemResolver(views.Views));

        var byId = (await service.Handle(new StreamEventsRequest(After: 0, Ref: ItemRef.To(other)), ct)).Value;
        var byAlias = (await service.Handle(new StreamEventsRequest(After: 0, Ref: ItemRef.To(alias)), ct)).Value;
        var unknown = await service.Handle(new StreamEventsRequest(After: 0, Ref: ItemRef.To(ItemAlias.From("ENG-999"))), ct);

        Assert.Equal([2L], byId.Events.Select(e => e.Sequence));
        Assert.Equal([1L], byAlias.Events.Select(e => e.Sequence));
        Assert.Equal(SindriErrors.ItemNotFound.Code, unknown.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_naming_another_repos_item_streams_it()
    {
        var views = new ViewsHarness();
        var h = views.Slice;
        var created = new ItemCreated(h.Ids.New<CommandId>(), A.FirstItemId, "t", RepoName.From("elsewhere"), Link: null, Body: null, Owner: null);
        await Append(h, await h.HoldLease(), EventTypes.ItemCreated, created, "item.created:1", A.FirstItemId.Value);   // an id resolves through the ledger
        await views.Fold(EventTypes.ItemCreated, created, A.FirstItemId);
        using var entered = h.Callers.Enter(new CallerContextBuilder().Session().InRepo("demo").Build());
        var service = StreamEventsServices.Over(h, new PassThroughScrubber(), new ViewItemResolver(views.Views));

        var result = await service.Handle(new StreamEventsRequest(After: 0, Ref: ItemRef.To(A.FirstItemId)), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Session_without_a_repo_sees_every_event()
    {
        var h = new SliceHarness();
        var grant = await h.HoldLease();
        var ct = TestContext.Current.CancellationToken;
        var created = new ItemCreated(h.Ids.New<CommandId>(), A.FirstItemId, "t", RepoName.From("demo"), Link: null, Body: null, Owner: null);
        await Append(h, grant, EventTypes.ItemCreated, created, "item.created:1", A.FirstItemId.Value);
        await Append(h, grant, "service.heartbeat", new Heartbeat(h.Clock.GetUtcNow(), h.Clock.GetUtcNow()), "service.heartbeat:1:1");
        using var entered = h.Callers.Enter(new CallerContextBuilder().Session().Build());

        var page = (await StreamEventsServices.Over(h).Handle(new StreamEventsRequest(After: 0), ct)).Value;

        Assert.Equal([1L, 2L], page.Events.Select(e => e.Sequence));
        Assert.Equal(2L, page.ScannedTo);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_payload_naming_another_repo_reaches_a_session()
    {
        var h = new SliceHarness();
        var grant = await h.HoldLease();
        var ct = TestContext.Current.CancellationToken;
        await Append(h, grant, EventTypes.ContextMapPromoted, new ContextMapPromoted(1, "elsewhere", Retained: null), "context.map.promoted:elsewhere:v1");
        await Append(h, grant, EventTypes.ContextMapPromoted, new ContextMapPromoted(1, "demo", Retained: null), "context.map.promoted:demo:v1");
        using var entered = h.Callers.Enter(new CallerContextBuilder().Session().InRepo("demo").Build());

        var page = (await StreamEventsServices.Over(h).Handle(new StreamEventsRequest(After: 0), ct)).Value;

        Assert.Equal([1L, 2L], page.Events.Select(e => e.Sequence));
        Assert.Equal(2L, page.ScannedTo);
    }

    private static async Task Append<TPayload>(SliceHarness h, LeaseGrant grant, string type, TPayload payload, string key, string? item = null) =>
        _ = await h.Store.Append(h.Events.Entry(type, payload, key, item), grant, TestContext.Current.CancellationToken);

    private sealed class MaskScrubber(string secret) : ITextScrubber
    {
        public string Scrub(string text) => text.Replace(secret, "[masked]", StringComparison.Ordinal);
    }
}
