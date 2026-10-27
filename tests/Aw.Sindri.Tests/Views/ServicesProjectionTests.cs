using Aw.Kernel.Ids;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Views;

/// <summary>The 'services' fold (API spec §2.3): 'service.ran' → the service's row with its last run.</summary>
public sealed class ServicesProjectionTests
{
    private static readonly ServiceName _heartbeat = ServiceName.From("heartbeat");
    private readonly SliceHarness _h = new();

    [Fact, Trait("Lane", "unit")]
    public void Services_fold_keeps_the_last_run()
    {
        var slot = _h.Clock.GetUtcNow();
        var first = Ran(slot, ServiceOutcome.Ok, code: null, sequence: 3);
        _h.Clock.Advance(TimeSpan.FromSeconds(30));
        var second = Ran(slot.AddSeconds(30), ServiceOutcome.Ok, code: null, sequence: 7);

        var row = ServicesProjection.Fold(ServicesProjection.Fold(row: null, Payload(first), first), Payload(second), second);

        Assert.Equal(new ServiceRow(_heartbeat, new LastRun(slot.AddSeconds(30), ServiceOutcome.Ok, Code: null, second.Envelope.OccurredAt), Paused: false, 7, ScheduledSlot: slot.AddSeconds(30)), row);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_failure_then_a_success_reads_ok()
    {
        var slot = _h.Clock.GetUtcNow();
        var failed = Ran(slot, ServiceOutcome.Failed, "SND-SCHED-001", sequence: 1);
        var ok = Ran(slot.AddSeconds(5), ServiceOutcome.Ok, code: null, sequence: 2);

        var afterFailure = ServicesProjection.Fold(row: null, Payload(failed), failed);
        var afterSuccess = ServicesProjection.Fold(afterFailure, Payload(ok), ok);

        Assert.Equal((ServiceOutcome.Failed, "SND-SCHED-001"), (afterFailure.LastRun!.Outcome, afterFailure.LastRun.Code));
        Assert.Equal(ServiceOutcome.Ok, afterSuccess.LastRun!.Outcome);
        Assert.Null(afterSuccess.LastRun.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void Pause_then_resume_folds_paused()
    {
        var slot = _h.Clock.GetUtcNow();
        var ran = Ran(slot, ServiceOutcome.Ok, code: null, sequence: 1);
        var paused = Recorded.Of(_h.Events, EventTypes.ServicePaused, new ServicePaused(_h.Ids.New<CommandId>(), _heartbeat), 2);
        var resumed = Recorded.Of(_h.Events, EventTypes.ServiceResumed, new ServiceResumed(_h.Ids.New<CommandId>(), _heartbeat), 3);

        var afterRun = ServicesProjection.Fold(row: null, Payload(ran), ran);
        var afterPause = ServicesProjection.Fold(afterRun, _h.Events.Payload<ServicePaused>(paused).Value, paused);
        var afterResume = ServicesProjection.Fold(afterPause, _h.Events.Payload<ServiceResumed>(resumed).Value, resumed);

        Assert.True(afterPause.Paused);
        Assert.Equal((afterRun.LastRun, afterRun.ScheduledSlot, 2L), (afterPause.LastRun, afterPause.ScheduledSlot, afterPause.Sequence));
        Assert.False(afterResume.Paused);
        Assert.Equal((afterRun.LastRun, 3L), (afterResume.LastRun, afterResume.Sequence));
    }

    [Fact, Trait("Lane", "unit")]
    public void Pausing_a_service_that_never_ran_makes_a_row_with_no_last_run()
    {
        var paused = Recorded.Of(_h.Events, EventTypes.ServicePaused, new ServicePaused(_h.Ids.New<CommandId>(), _heartbeat), 4);

        var row = ServicesProjection.Fold(row: null, _h.Events.Payload<ServicePaused>(paused).Value, paused);

        Assert.Equal(new ServiceRow(_heartbeat, LastRun: null, Paused: true, 4, PausedBy: paused.Envelope.Actor, PausedAt: paused.Envelope.OccurredAt), row);
    }

    [Fact, Trait("Lane", "unit")]
    public void Manual_run_keeps_the_scheduled_slot()
    {
        var slot = _h.Clock.GetUtcNow();
        var scheduled = Ran(slot, ServiceOutcome.Ok, code: null, sequence: 1);
        var manual = Recorded.Of(_h.Events, EventTypes.ServiceRan,
            new ServiceRan(_h.Ids.New<CommandId>(), _heartbeat, slot.AddSeconds(7), ServiceOutcome.Failed, "SND-SCHED-001", A.FirstJobId), 2);

        var afterScheduled = ServicesProjection.Fold(row: null, Payload(scheduled), scheduled);
        var afterManual = ServicesProjection.Fold(afterScheduled, Payload(manual), manual);

        Assert.Equal(slot, afterManual.ScheduledSlot);
        Assert.Equal((slot.AddSeconds(7), ServiceOutcome.Failed), (afterManual.LastRun!.Slot, afterManual.LastRun.Outcome));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_manual_run_before_any_scheduled_one_leaves_the_slot_null()
    {
        var manual = Recorded.Of(_h.Events, EventTypes.ServiceRan,
            new ServiceRan(_h.Ids.New<CommandId>(), _heartbeat, _h.Clock.GetUtcNow(), ServiceOutcome.Ok, Code: null, A.FirstJobId), 1);

        Assert.Null(ServicesProjection.Fold(row: null, Payload(manual), manual).ScheduledSlot);
    }

    private Core.Ledger.LedgerEvent Ran(DateTimeOffset slot, ServiceOutcome outcome, string? code, long sequence) =>
        Recorded.Of(_h.Events, EventTypes.ServiceRan, new ServiceRan(_h.Ids.New<CommandId>(), _heartbeat, slot, outcome, code), sequence);

    private ServiceRan Payload(Core.Ledger.LedgerEvent recorded) => _h.Events.Payload<ServiceRan>(recorded).Value;
}
