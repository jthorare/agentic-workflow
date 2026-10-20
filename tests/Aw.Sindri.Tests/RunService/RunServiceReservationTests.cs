using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.RunService;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Server;

namespace Aw.Sindri.Tests.RunService;

/// <summary>A manual run's claim is taken at admission, inside the gate, so a scheduled tick cannot slip in between the 200 and the work (API P6 review T12).</summary>
[Lane(TestLane.Unit)]
public sealed class RunServiceReservationTests
{
    private static readonly ServiceName _tidy = ServiceName.From("tidy");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<(SliceHarness H, ServiceRunGuard Guard, RunServiceAdmission Admission)> Admission()
    {
        var h = new SliceHarness();
        _ = await h.Profiles.Save(SliceHarness.ProfileOf("thor", ("tidy", "5s")), Token);
        var guard = new ServiceRunGuard();
        return (h, guard, new RunServiceAdmission(h.Profiles, [new ScriptedService(ScriptedService.Worked)], [], guard));
    }

    [Fact]
    public async Task A_scheduled_tick_cannot_claim_between_the_admission_and_the_work()
    {
        var (_, guard, admission) = await Admission();
        var request = A.RunServiceRequest().ForService(_tidy).Build();

        var admitted = await admission.Admit(request, Token);
        var tick = guard.TryClaim(_tidy, out var scheduled);   // the scheduler's tick, after the 200 and before the work

        Assert.True(admitted.IsSuccess);
        Assert.False(tick);
        scheduled.Dispose();
        Assert.True(guard.TryClaimReserved(_tidy, out var work));   // the work takes over what the admission reserved
        Assert.True(guard.IsManualRun(_tidy));
        work.Dispose();
        Assert.True(guard.TryClaim(_tidy, out var after));
        after.Dispose();
    }

    [Fact]
    public async Task A_refused_start_gives_the_reservation_back()
    {
        var (_, guard, admission) = await Admission();
        var request = A.RunServiceRequest().ForService(_tidy).Build();
        _ = await admission.Admit(request, Token);

        admission.Rollback(request);

        Assert.True(guard.TryClaim(_tidy, out var free));
        free.Dispose();
    }

    [Fact]
    public async Task A_second_admission_that_is_refused_never_gives_back_the_first_ones_reservation()
    {
        var (h, guard, first) = await Admission();
        var second = new RunServiceAdmission(h.Profiles, [new ScriptedService(ScriptedService.Worked)], [], guard);
        var request = A.RunServiceRequest().ForService(_tidy).Build();
        _ = await first.Admit(request, Token);
        _ = await second.Admit(request, Token);   // a manual run is reserved: this one is refused by the registry later

        second.Rollback(request);

        Assert.False(guard.TryClaim(_tidy, out _));   // still the first start's
        Assert.True(guard.TryClaimReserved(_tidy, out var work));
        work.Dispose();
    }

    [Fact]
    public async Task A_scheduled_run_in_flight_is_refused_at_admission_with_the_one_message()
    {
        var (_, guard, admission) = await Admission();
        Assert.True(guard.TryClaim(_tidy, out var scheduled));
        using (scheduled)
        {
            var refused = await admission.Admit(A.RunServiceRequest().ForService(_tidy).Build(), Token);

            Expect.Equal(SindriErrors.JobConflict.Code, refused.Error.Code);
            Expect.Equal(RunServiceMessages.ScheduledInFlight(_tidy).Message, refused.Error.Message);
        }
    }
}
