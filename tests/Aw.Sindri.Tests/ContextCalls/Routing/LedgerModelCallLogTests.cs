using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Host.Models;
using Aw.Sindri.Tests.Server;

namespace Aw.Sindri.Tests.ContextCalls.Routing;

/// <summary>The router's event writer (model routing spec §4 "Writing events"): one ledger entry per call, best effort.</summary>
public sealed class LedgerModelCallLogTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ContextModelCalled Called(string id = "call-1") => new(ModelCallId.From(id), Job: null, "labeling", "labeling",
        [new ModelCallTry("claude-code", "haiku", ModelCallOutcome.Answered, Reason: null, 12, "builtin")], new ModelCallAnswerer("claude-code", "haiku"),
        10, 5, ModelTokenBasis.Estimated, Items: null, How: null, DateTimeOffset.UnixEpoch, 12);

    [Fact, Trait("Lane", "unit")]
    public async Task The_event_is_appended_under_its_call_id()
    {
        var h = new SliceHarness();
        _ = await h.HoldLease();
        var log = new LedgerModelCallLog(h.Held, h.Store, h.Events, new ListLogger<LedgerModelCallLog>());

        await log.Record(Called(), Token);

        var row = Assert.Single(h.Store.Events, static e => string.Equals(e.Envelope.Type, EventTypes.ContextModelCalled, StringComparison.Ordinal));
        Assert.Equal("context.model.called:call-1", row.IdempotencyKey);
        Assert.Equal("call-1", h.Events.Payload<ContextModelCalled>(row).Value.CallId.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_lost_lease_or_no_control_plane_is_logged_and_throws_nothing()
    {
        var h = new SliceHarness();   // no lease held
        var logger = new ListLogger<LedgerModelCallLog>();
        var log = new LedgerModelCallLog(h.Held, h.Store, h.Events, logger);

        await log.Record(Called(), Token);

        Assert.DoesNotContain(h.Store.Events, static e => string.Equals(e.Envelope.Type, EventTypes.ContextModelCalled, StringComparison.Ordinal));
        _ = Assert.Single(logger.Lines);
    }
}
