using Aw.Kernel;
using Aw.Kernel.Events;
using Aw.Sindri.Core.Health;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Host.Server;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Server;

namespace Aw.Sindri.Tests.Views;

/// <summary>The projector status never reads Healthy before a real pass has published, and a pass that throws shows as stalled (#2053).</summary>
public sealed class ProjectorHealthyOnlyAfterPassTests
{
    private readonly SliceHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public void A_projector_that_has_not_published_yet_never_reads_healthy()
    {
        var progress = new ProjectorProgress(_h.Clock);   // serve just booted: no pass has published, views.db may say stalled

        var health = HealthSummary.For(running: true, progress.Current, [], ProfileApproval.Approved);

        Assert.NotEqual(HealthWord.Healthy, health.Word);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pass_that_throws_publishes_stalled_with_the_exception_type_after_an_earlier_healthy_publish()
    {
        var grant = await _h.HoldLease();
        var context = EventContext.Create("host:sindri", "local", 0, _h.Ids.NewId()).Value;
        _ = await _h.Store.Append(new LedgerEntry(context.Envelope(ToggleProjection.Put, """{"n":1}""", _h.Clock, _h.Ids), "k0001", Item: null), grant, Token);
        var ledger = new ThrowingHeadLedgerReader(_h.Store);
        var progress = new ProjectorProgress(_h.Clock);
        var projector = new Projector(ledger, new InMemoryViewStore(SliceHarness.Json), [new ToggleProjection()], progress, _h.Callers, _h.Clock, new ListLogger<Projector>());
        using var host = new ProjectorHost(projector, new LedgerSignal(_h.Clock), _h.Clock, new ListLogger<ProjectorHost>());

        await host.StartAsync(Token);
        await Until(() => progress.Current.Cursor == 1);
        Assert.False(progress.Current.Stalled);
        ledger.Throw = true;
        await Until(() => progress.Current.Stalled);   // the fake clock ticks the host's one-second poll

        await host.StopAsync(Token);
        var shown = progress.Current;
        Assert.True(shown is { Stalled: true, StalledAt: null });
        Assert.Contains(nameof(InvalidOperationException), shown.StalledType, StringComparison.Ordinal);
        var health = HealthSummary.For(running: true, shown, [], ProfileApproval.Approved);
        Assert.NotEqual(HealthWord.Healthy, health.Word);
    }

    private async Task Until(Func<bool> done)
    {
        for (var i = 0; i < 300 && !done(); i++)
        {
            _h.Clock.Advance(TimeSpan.FromSeconds(1));
            await Task.Delay(10, Token);
        }
    }

    /// <summary>A ledger reader whose head read throws a plain exception (not a Result) once switched on.</summary>
    private sealed class ThrowingHeadLedgerReader(ILedgerReader inner) : ILedgerReader
    {
        public bool Throw { get; set; }

        public ValueTask<Result<IReadOnlyList<LedgerEvent>>> ReadAfter(long sequence, int max, CancellationToken cancellationToken) => inner.ReadAfter(sequence, max, cancellationToken);

        public ValueTask<Result<LedgerScan>> Scan(long sequence, int max, LedgerFilter filter, CancellationToken cancellationToken) => inner.Scan(sequence, max, filter, cancellationToken);

        public ValueTask<Result<LedgerEvent?>> Latest(string type, CancellationToken cancellationToken) => inner.Latest(type, cancellationToken);

        public ValueTask<Result<LedgerEvent?>> ByKey(string idempotencyKey, CancellationToken cancellationToken) => inner.ByKey(idempotencyKey, cancellationToken);

        public ValueTask<Result<long>> Head(CancellationToken cancellationToken) =>
            Throw ? throw new InvalidOperationException("the head read blew up (fixture)") : inner.Head(cancellationToken);
    }
}
