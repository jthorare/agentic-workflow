using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Features.ReadLedger;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.StreamEvents;

namespace Aw.Sindri.Tests.ReadLedger;

public sealed class ReadLedgerTests
{
    private readonly SliceHarness _h = new();

    [Fact, Trait("Lane", "unit")]
    public async Task ReadLedger_pages_public_envelopes_after_a_cursor_with_sanitized_payloads()
    {
        var grant = await _h.HoldLease();
        var created = new ItemCreated(_h.Ids.New<CommandId>(), A.FirstItemId, "Fix \u001b[31mSECRET\u001b[0m", Repo: null, Link: null, Body: "private SECRET body", Owner: null);
        await Append(grant, EventTypes.ItemCreated, created, "item.created:1", A.FirstItemId.Value);
        await Heartbeat(grant, 1);
        await Heartbeat(grant, 2);
        await Heartbeat(grant, 3);

        var page = (await Service(new MaskScrubber()).Handle(A.ReadLedgerRequest().After(0).WithMax(2).Build(), TestContext.Current.CancellationToken)).Value;

        Assert.Equal([1L, 2L], page.Events.Select(e => e.Sequence));
        Assert.Equal(2L, page.Next);
        Assert.False(page.Events[0].Payload.TryGetProperty("body", out _));
        Assert.Equal("Fix \\x1B[31m[masked]\\x1B[0m", page.Events[0].Payload.GetProperty("title").GetString());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Next_pages_to_the_end()
    {
        var grant = await _h.HoldLease();
        for (var i = 1; i <= 5; i++)
        {
            await Heartbeat(grant, i);
        }

        var seen = new List<long>();
        long? next = 0;
        while (next is { } after)
        {
            var page = (await Service().Handle(A.ReadLedgerRequest().After(after).WithMax(2).Build(), TestContext.Current.CancellationToken)).Value;
            seen.AddRange(page.Events.Select(e => e.Sequence));
            next = page.Next;
        }

        Assert.Equal([1L, 2L, 3L, 4L, 5L], seen);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_full_page_names_a_next_cursor_even_when_appends_made_the_head_it_read_stale()
    {
        var grant = await _h.HoldLease();
        for (var i = 1; i <= 4; i++)
        {
            await Heartbeat(grant, i);
        }

        var service = new ReadLedgerService(new StaleHead(_h.Store, 2), StreamEventsServices.View(_h, new PassThroughScrubber()), new NoItems(), StreamEventsServices.Repos(_h), _h.Callers);   // the head read before 3 and 4 committed

        var page = (await service.Handle(A.ReadLedgerRequest().After(0).WithMax(2).Build(), TestContext.Current.CancellationToken)).Value;

        Assert.Equal([1L, 2L], page.Events.Select(e => e.Sequence));
        Assert.Equal(2L, page.Next);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Human_sees_command_handled()
    {
        var grant = await _h.HoldLease();
        await CommandHandledRow(grant);
        await Heartbeat(grant, 1);
        using var entered = _h.Callers.Enter(new CallerContextBuilder().Human().Build());

        var page = (await Service().Handle(A.ReadLedgerRequest().Build(), TestContext.Current.CancellationToken)).Value;

        Assert.Equal(["command.handled", "service.heartbeat"], page.Events.Select(e => e.Type), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Session_never_does()
    {
        var grant = await _h.HoldLease();
        await CommandHandledRow(grant);
        await Heartbeat(grant, 1);
        using var entered = _h.Callers.Enter(new CallerContextBuilder().Session().InRepo("demo").Build());

        var page = (await Service().Handle(A.ReadLedgerRequest().Build(), TestContext.Current.CancellationToken)).Value;

        Assert.Equal(["service.heartbeat"], page.Events.Select(e => e.Type), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Type_filter_keeps_the_matching_types()
    {
        var grant = await _h.HoldLease();
        await CommandHandledRow(grant);
        await Heartbeat(grant, 1);
        using var entered = _h.Callers.Enter(new CallerContextBuilder().Human().Build());

        var page = (await Service().Handle(A.ReadLedgerRequest().WithType("command.*").Build(), TestContext.Current.CancellationToken)).Value;

        Assert.Equal([1L], page.Events.Select(e => e.Sequence));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_sees_only_its_repos_items()
    {
        var grant = await _h.HoldLease();
        foreach (var (item, repo) in new[] { (A.FirstItemId, "demo"), (ItemId.From("ITM-00000000000000000000000000000002"), "elsewhere") })
        {
            var created = new ItemCreated(_h.Ids.New<CommandId>(), item, "t", RepoName.From(repo), Link: null, Body: null, Owner: null);
            await Append(grant, EventTypes.ItemCreated, created, "item.created:" + item.Value, item.Value);
        }

        using var entered = _h.Callers.Enter(new CallerContextBuilder().Session().InRepo("demo").Build());
        var page = (await Service().Handle(A.ReadLedgerRequest().Build(), TestContext.Current.CancellationToken)).Value;

        Assert.Equal([1L], page.Events.Select(e => e.Sequence));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(10001)]
    [InlineData(0)]
    public async Task Max_outside_1_to_10000_is_400(int max)
    {
        var result = await Service().Handle(A.ReadLedgerRequest().WithMax(max).Build(), TestContext.Current.CancellationToken);

        Assert.Equal(SindriErrors.InvalidInput.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task After_past_the_head_is_400()
    {
        var result = await Service().Handle(A.ReadLedgerRequest().After(5).Build(), TestContext.Current.CancellationToken);

        Assert.Equal(SindriErrors.InvalidInput.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_negative_after_is_named_as_negative()
    {
        var result = await Service().Handle(A.ReadLedgerRequest().After(-1).Build(), TestContext.Current.CancellationToken);

        Assert.Equal(SindriErrors.InvalidInput.Code, result.Error.Code);
        Assert.StartsWith("after -1 is negative; a cursor is 0 or more", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_bad_type_glob_is_400()
    {
        var result = await Service().Handle(A.ReadLedgerRequest().WithType("Item.*").Build(), TestContext.Current.CancellationToken);

        Assert.Equal(SindriErrors.InvalidInput.Code, result.Error.Code);
    }

    private ReadLedgerService Service(ITextScrubber? scrubber = null) =>
        new(_h.Store, StreamEventsServices.View(_h, scrubber ?? new PassThroughScrubber()), new NoItems(), StreamEventsServices.Repos(_h), _h.Callers);

    private Task Heartbeat(LeaseGrant grant, int slot)
    {
        var at = _h.Clock.GetUtcNow().AddSeconds(slot);
        return Append(grant, "service.heartbeat", new Heartbeat(at, at), "service.heartbeat:1:" + slot.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private Task CommandHandledRow(LeaseGrant grant) =>
        Append(grant, EventTypes.CommandHandled,
            new CommandHandled(_h.Ids.New<CommandId>(), ActionId.From("CreateItem"), ActionClass.Desk, "human:cli", CommandOutcome.Accepted, Code: null), "command.handled:1");

    private async Task Append<TPayload>(LeaseGrant grant, string type, TPayload payload, string key, string? item = null) =>
        _ = await _h.Store.Append(_h.Events.Entry(type, payload, key, item), grant, TestContext.Current.CancellationToken);

    /// <summary>A ledger whose head is fixed, as one read before later appends committed.</summary>
    private sealed class StaleHead(ILedgerReader inner, long head) : ILedgerReader
    {
        public ValueTask<Result<IReadOnlyList<LedgerEvent>>> ReadAfter(long sequence, int max, CancellationToken cancellationToken) => inner.ReadAfter(sequence, max, cancellationToken);

        public ValueTask<Result<LedgerScan>> Scan(long sequence, int max, LedgerFilter filter, CancellationToken cancellationToken) => inner.Scan(sequence, max, filter, cancellationToken);

        public ValueTask<Result<LedgerEvent?>> Latest(string type, CancellationToken cancellationToken) => inner.Latest(type, cancellationToken);

        public ValueTask<Result<long>> Head(CancellationToken cancellationToken) => ValueTask.FromResult<Result<long>>(head);
    }

    private sealed class MaskScrubber : ITextScrubber
    {
        public string Scrub(string text) => text.Replace("SECRET", "[masked]", StringComparison.Ordinal);
    }
}
