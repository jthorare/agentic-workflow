using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Features.DeferOnyx;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Views;

namespace Aw.Sindri.Tests.DeferOnyx;

/// <summary>Leaving Onyx for later (setup-once spec §5.6): one 'onyx.deferred' per command, with the choice in force.</summary>
[Lane(TestLane.Unit)]
public sealed class DeferOnyxTests
{
    private readonly ViewsHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private DeferOnyxService Service() => new(_h.Slice.Store, _h.Slice.Held, _h.Slice.Events, _h.Slice.Callers);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DeferOnyx_records_the_choice_keyed_by_the_command(bool deferred)
    {
        _ = await _h.Slice.HoldLease();
        using var entered = _h.Slice.Callers.Enter(A.Caller().Human().Build());

        var result = await Service().Handle(new DeferOnyxRequest(deferred), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        var recorded = Assert.Single(_h.Slice.Store.Events);
        var payload = _h.Slice.Events.Payload<OnyxDeferred>(recorded).Value;
        Expect.Equal((EventTypes.OnyxDeferred, deferred), (recorded.Envelope.Type, payload.Deferred));
        Expect.Equal($"onyx.deferred:{payload.CommandId.Value}", recorded.IdempotencyKey);
        Expect.Equal((deferred, recorded.Sequence), (result.Value.Deferred, result.Value.Sequence));
    }

    [Fact]
    public async Task Without_the_lease_nothing_is_recorded()
    {
        using var entered = _h.Slice.Callers.Enter(A.Caller().Human().Build());

        var result = await Service().Handle(new DeferOnyxRequest(Deferred: true), Token);

        Assert.True(result.IsError);
        Expect.Equal(SindriErrors.NotRunning.Code, result.Error.Code);
        Assert.Empty(_h.Slice.Store.Events);
    }
}
