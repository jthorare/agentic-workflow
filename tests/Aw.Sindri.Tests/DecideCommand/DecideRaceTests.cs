using Aw.Kernel.Ids;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Requests;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.Decide;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Ledger;
using Aw.Sindri.Tests.Views;

namespace Aw.Sindri.Tests.DecideCommand;

/// <summary>Two humans deciding the same decision together, on the real SQLite ledger and views (P4 plan S4, risk "concurrency test flakes"): the unique key leaves exactly one 'decision.recorded', the loser meets SND-ITEM-409. Counts are asserted, never the order.</summary>
public sealed class DecideRaceTests : IDisposable
{
    private const int Rounds = 8;
    private readonly TempHome _home = new();
    private readonly SliceHarness _slice = new();

    public void Dispose() => _home.Dispose();

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task Two_concurrent_decides_one_event_one_409_on_sqlite()
    {
        var ct = TestContext.Current.CancellationToken;
        var stores = SqliteStores.Create(_home.Path, _slice.Clock, _slice.Probe);
        var held = new Core.Lease.HeldLease();
        held.Hold((await stores.Lease.Acquire(_slice.Probe.Self(), ct)).Value);
        var decisions = new List<DecisionId>();
        for (var n = 0; n < Rounds; n++)
        {
            var (request, decision) = (RequestId.New(_slice.Ids), DecisionId.New(_slice.Ids));
            decisions.Add(decision);
            var filed = new RequestFiled(_slice.Ids.New<CommandId>(), request, decision, RequestKind.Note, Item: null, Repo: null, "x");
            _ = (await stores.Ledger.Append(_slice.Events.Entry(EventTypes.RequestFiled, filed, $"request.filed:{request.Value}"), held.Current!, ct)).Value;
        }

        using var views = new SqliteViews(_home.Path);
        _ = (await views.Store.Open(ViewSchema.Version, ct)).Value;
        var filedEvents = (await stores.Ledger.ReadAfter(0, 100, ct)).Value;
        var applied = (await views.Store.Apply(filedEvents, [new RequestsProjection(_slice.Events, new PassThroughScrubber())], _slice.Clock.GetUtcNow(), ct)).Value;
        Assert.Null(applied.Stall);
        var service = new DecideService(views.Reader, stores.Ledger, held, _slice.Events, new PassThroughScrubber(), _slice.Callers);

        foreach (var decision in decisions)
        {
            using var barrier = new Barrier(2);
            var both = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Task.Run(async () =>
            {
                using var entered = _slice.Callers.Enter(A.Caller().Human().Build());
                barrier.SignalAndWait(ct);
                return await service.Handle(A.DecideRequest().ForDecision(decision).Build(), ct);
            }, ct)));

            Assert.Equal(1, both.Count(r => r.IsSuccess));
            var lost = Assert.Single(both, r => r.IsError);
            Assert.Equal(SindriErrors.ItemConflict.Code, lost.Error.Code);
        }

        var recorded = (await stores.Ledger.ReadAfter(0, 1000, ct)).Value.Where(e => e.Envelope.Type == EventTypes.DecisionRecorded).ToList();
        Assert.Equal(Rounds, recorded.Count);
        Assert.Equal(decisions.Select(d => $"decision.recorded:{d.Value}").Order(StringComparer.Ordinal), recorded.Select(e => e.IdempotencyKey).Order(StringComparer.Ordinal));
    }
}
