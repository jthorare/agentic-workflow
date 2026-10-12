using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Features.StartProcess;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.StartProcess;

public sealed class StartProcessTests
{
    private readonly SliceHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private StartProcessService Service() => new(_h.Store, _h.Probe, _h.Held, _h.Profiles, _h.Store, _h.Events);

    [Fact, Trait("Lane", "unit")]
    public async Task StartProcess_takes_the_lease_holds_it_and_records_the_start_once()
    {
        var started = (await Service().Handle(new StartProcessRequest(), Token)).Value;

        Assert.Equal(1, started.Grant.Epoch);
        Assert.Same(started.Grant, _h.Held.Current);
        var recorded = Assert.Single(_h.Store.Events);
        Assert.Equal((EventTypes.ProcessStarted, "process.started:1", "host:sindri", "local", 0),
            (recorded.Envelope.Type, recorded.IdempotencyKey, recorded.Envelope.Actor, recorded.Envelope.Tenant, recorded.Envelope.Ring));
        Assert.Contains("\"epoch\":1", recorded.Envelope.Payload, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task StartProcess_is_refused_while_another_live_process_holds_the_lease()
    {
        var other = FakeProcessProbe.Holder(200);
        _h.Probe.Start(other);
        _ = await _h.Store.Acquire(other, Token);

        var refused = await Service().Handle(new StartProcessRequest(), Token);

        Assert.Equal(SindriErrors.LeaseHeld.Code, refused.Error.Code);
        Assert.Null(_h.Held.Current);
        Assert.Empty(_h.Store.Events);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task StartProcess_gives_the_lease_back_when_the_start_cant_be_recorded()
    {
        var failed = await new StartProcessService(_h.Store, _h.Probe, _h.Held, _h.Profiles, new AppendFailingLedger(_h.Store), _h.Events)
            .Handle(new StartProcessRequest(), Token);

        Assert.Equal(AppendFailingLedger.Failure, failed.Error);
        Assert.Null(_h.Held.Current);
        Assert.True((await _h.Store.Read(Token)).Value is { Released: true });
    }
}
