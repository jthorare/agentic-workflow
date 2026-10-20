using Aw.Kernel.Ids;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.RecordServiceRun;
using Aw.Sindri.Host.Server;
using Aw.Sindri.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Server;

/// <summary>
/// The idle rule (API spec C8): a run that failed or did work is recorded as 'service.ran', and so is the first ok after
/// a failure; an idle run after an ok one writes nothing. The scheduler runs on the fake clock, as in
/// <see cref="SchedulerHostTests"/>, with a service that answers from a script.
/// </summary>
public sealed class IdleRunTests
{
    private readonly SliceHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task The_first_idle_ok_after_a_failure_is_recorded_and_clears_the_failure_in_the_services_view()
    {
        var tidy = new ScriptedService(ScriptedService.Failed, ScriptedService.Quiet, ScriptedService.Quiet);

        await Run(tidy, runs: 3);

        Assert.Equal([ServiceOutcome.Failed, ServiceOutcome.Ok], Rows().Select(r => r.Outcome));
        Assert.Equal(ServiceOutcome.Ok, ViewRow().LastRun!.Outcome);
        Assert.Null(ViewRow().LastRun!.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_failure_row_stands_after_the_failure_and_is_gone_after_the_first_idle_ok_the_scheduler_records()
    {
        await Run(new ScriptedService(ScriptedService.Failed, ScriptedService.Quiet, ScriptedService.Quiet), runs: 3);
        var views = new InMemoryViewStore(SliceHarness.Json);
        _ = await views.Open(ViewSchema.Version, Token);
        var rowsAfterEach = new List<int>();

        foreach (var recorded in _h.Store.Events.Where(e => e.Envelope.Type == EventTypes.ServiceRan))
        {
            _ = await views.Apply([recorded], [new NeedsYouProjection(_h.Events, new PassThroughScrubber(), [])], _h.Clock.GetUtcNow(), Token);
            rowsAfterEach.Add((await views.List<NeedsYouRowRecord>(ViewNames.NeedsYou, Token)).Value.Count);
        }

        Assert.Equal([1, 0], rowsAfterEach);   // the scheduler records the failure and the first quiet run only: the second quiet run writes nothing
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_idle_run_after_an_ok_one_writes_nothing()
    {
        var tidy = new ScriptedService(ScriptedService.Worked, ScriptedService.Quiet, ScriptedService.Quiet, ScriptedService.Quiet);

        await Run(tidy, runs: 4);

        _ = Assert.Single(Rows());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_run_that_did_work_after_an_ok_one_writes_a_row()
    {
        var tidy = new ScriptedService(ScriptedService.Worked, ScriptedService.Quiet, ScriptedService.Worked);

        await Run(tidy, runs: 3);

        Assert.Equal(2, Rows().Count);
        Assert.NotEqual(Rows()[0].Slot, Rows()[1].Slot);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failure_always_writes_a_row()
    {
        var tidy = new ScriptedService(ScriptedService.Worked, ScriptedService.Quiet, ScriptedService.Failed, ScriptedService.Failed);

        await Run(tidy, runs: 4);

        Assert.Equal([ServiceOutcome.Ok, ServiceOutcome.Failed, ServiceOutcome.Failed], Rows().Select(r => r.Outcome));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task After_a_restart_the_last_failure_is_remembered_so_the_first_idle_ok_is_recorded()
    {
        await Run(new ScriptedService(ScriptedService.Failed), runs: 1);
        await Run(new ScriptedService(ScriptedService.Quiet, ScriptedService.Quiet), runs: 2);

        Assert.Equal([ServiceOutcome.Failed, ServiceOutcome.Ok], Rows().Select(r => r.Outcome));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task After_a_restart_the_last_ok_is_remembered_so_an_idle_run_writes_nothing()
    {
        await Run(new ScriptedService(ScriptedService.Worked), runs: 1);
        await Run(new ScriptedService(ScriptedService.Quiet, ScriptedService.Quiet), runs: 2);

        _ = Assert.Single(Rows());
    }

    private List<ServiceRan> Rows() => [.. _h.Store.Events
        .Where(e => e.Envelope.Type == EventTypes.ServiceRan)
        .Select(e => _h.Events.Payload<ServiceRan>(e).Value)];

    /// <summary>The 'services' row for 'tidy' after folding every recorded run in order.</summary>
    private ServiceRow ViewRow()
    {
        ServiceRow? row = null;
        foreach (var e in _h.Store.Events.Where(e => e.Envelope.Type == EventTypes.ServiceRan))
        {
            row = ServicesProjection.Fold(row, _h.Events.Payload<ServiceRan>(e).Value, e);
        }

        return row!;
    }

    /// <summary>Starts a scheduler (a fresh one is a restart), steps the clock until the service has run <paramref name="runs"/> times, and stops it.</summary>
    private async Task Run(ScriptedService tidy, int runs)
    {
        if (_h.Held.Current is null)
        {
            _ = await _h.Profiles.Save(SliceHarness.ProfileOf("thor", ("tidy", "5s")), Token);
            _ = await _h.HoldLease();
        }

        await using var provider = new ServiceCollection()
            .AddSingleton<ILedgerStore>(_h.Store)
            .AddSingleton<ILedgerReader>(_h.Store)
            .AddSingleton(_h.Events)
            .AddSingleton<TimeProvider>(_h.Clock)
            .AddSingleton(_h.Callers)
            .AddSingleton<IIdGenerator>(_h.Ids)
            .AddScoped<IService<RecordServiceRunRequest, RecordServiceRunResponse>, RecordServiceRunService>()
            .AddSingleton<IScheduledService>(tidy)
            .BuildServiceProvider();
        var clock = new SteppedClock(_h.Clock);
        using var scheduler = new SchedulerHost(provider.GetRequiredService<IServiceScopeFactory>(), new ScheduleInputs(new CountingApprovals(_h.Profiles), [], new NoPauses()), new ServiceRunGuard(), _h.Held, _h.Callers, clock, new ListLogger<SchedulerHost>());
        await scheduler.StartAsync(Token);
        for (var step = 0; tidy.Runs < runs; step++)
        {
            Assert.True(step < 60, "the service didn't run enough within 60 fake seconds");
            await clock.Step(TimeSpan.FromSeconds(1));
            await clock.Armed();
        }

        await scheduler.StopAsync(Token);
    }
}
