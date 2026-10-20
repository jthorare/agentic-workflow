using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Jobs;
using Aw.Sindri.Tests.Server;

namespace Aw.Sindri.Tests.RunService;

/// <summary>RunService as a job (API P6 unit D): the start's refusals, the manual run's events and actor, and what a run feeds back to the scheduler and the views.</summary>
[Lane(TestLane.Unit)]
public sealed class RunServiceTests
{
    private static readonly ServiceName _tidy = ServiceName.From("tidy");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Core.Commands.CallerContext Human() => A.Caller().Human().Build();

    [Fact]
    public async Task RunService_starts_a_job_that_runs_the_named_service_now()
    {
        var tidy = new ScriptedService(ScriptedService.Worked);
        await using var runtime = await RunServiceRuntime.Open(tidy);

        var started = await runtime.Start(A.RunServiceRequest().ForService(_tidy).Build(), Human());
        await runtime.Quiesce();

        Assert.True(started.IsSuccess, started.IsError ? started.Error.ToString() : null);
        Expect.Equal(1, tidy.Runs);
        var finished = runtime.H.Events.Payload<JobFinished>(Assert.Single(runtime.Of(EventTypes.JobFinished))).Value;
        Expect.Equal((JobOutcome.Succeeded, "ok: worked"), (finished.Outcome, finished.Summary));
    }

    [Fact]
    public async Task The_start_names_the_service_and_its_run_is_recorded_as_a_manual_service_ran_of_that_service()
    {
        await using var runtime = await RunServiceRuntime.Open(new ScriptedService(ScriptedService.Worked));

        var started = await runtime.Start(A.RunServiceRequest().ForService(_tidy).Build(), Human());
        await runtime.Quiesce();

        var job = started.Value.JobId;
        var startedEvent = runtime.H.Events.Payload<JobStarted>(Assert.Single(runtime.Of(EventTypes.JobStarted))).Value;
        Expect.Equal(_tidy, startedEvent.Service);
        var ranEvent = Assert.Single(runtime.Of(EventTypes.ServiceRan));
        var ran = runtime.H.Events.Payload<ServiceRan>(ranEvent).Value;
        Expect.Equal(($"service.ran:tidy:manual:{job.Value}", "service:tidy"), (ranEvent.IdempotencyKey, ranEvent.Envelope.Actor));
        Expect.Equal((_tidy, ServiceOutcome.Ok, job), (ran.Service, ran.Outcome, ran.Job));
    }

    [Fact]
    public async Task A_failing_service_is_a_failed_job_and_a_failed_service_ran_with_its_code()
    {
        var sour = new ScriptedService(ScriptedService.Failed);
        await using var runtime = await RunServiceRuntime.Open(sour);

        _ = await runtime.Start(A.RunServiceRequest().ForService(_tidy).Build(), Human());
        await runtime.Quiesce();

        var code = ScriptedService.Failure.Code;
        var finished = runtime.H.Events.Payload<JobFinished>(Assert.Single(runtime.Of(EventTypes.JobFinished))).Value;
        Expect.Equal((JobOutcome.Failed, code, ScriptedService.Failure.Message), (finished.Outcome, finished.Code, finished.Summary));
        var ran = runtime.H.Events.Payload<ServiceRan>(Assert.Single(runtime.Of(EventTypes.ServiceRan))).Value;
        Expect.Equal((ServiceOutcome.Failed, code), (ran.Outcome, ran.Code));
        Expect.Equal(ServiceOutcome.Failed, runtime.Get<ServiceRunGuard>().LastOutcome(_tidy));
    }

    [Theory]
    [InlineData("ghost")]
    [InlineData("missing")]
    public async Task Run_unknown_service_is_SND_SVC_404(string name)
    {
        await using var runtime = await RunServiceRuntime.Open(new ScriptedService(ScriptedService.Worked));   // 'ghost' is not in the profile; 'missing' is, but this build lacks it

        var started = await runtime.Start(A.RunServiceRequest().ForService(ServiceName.From(name)).Build(), Human());

        Expect.Equal(SindriErrors.ServiceNotFound.Code, started.Error.Code);
        Assert.Empty(runtime.Of(EventTypes.JobStarted));
    }

    [Fact]
    public async Task A_built_in_service_the_profile_does_not_name_runs_by_name()
    {
        var builtIn = new ScriptedService(ScriptedService.Worked);
        await using var runtime = await RunServiceRuntime.Open(builtIn, new BuiltInService(ServiceName.From("tidy"), new Cadence(TimeSpan.FromMinutes(1))), profile: false);

        var started = await runtime.Start(A.RunServiceRequest().ForService(_tidy).Build(), Human());
        await runtime.Quiesce();

        Assert.True(started.IsSuccess, started.IsError ? started.Error.ToString() : null);
        Expect.Equal(1, builtIn.Runs);
    }

    [Fact]
    public async Task Run_from_a_session_is_403_with_the_cli()
    {
        await using var runtime = await RunServiceRuntime.Open(new ScriptedService(ScriptedService.Worked));

        var started = await runtime.Start(A.RunServiceRequest().ForService(_tidy).Build(), A.Caller().Session().Build());

        Expect.Equal(SindriErrors.AuthRefused.Code, started.Error.Code);
        Assert.Contains("sindri services run <name>", started.Error.Message, StringComparison.Ordinal);
        Assert.Empty(runtime.Of(EventTypes.JobStarted));
    }

    [Fact]
    public async Task Run_while_a_scheduled_run_is_in_flight_is_409()
    {
        await using var runtime = await RunServiceRuntime.Open(new ScriptedService(ScriptedService.Worked));
        Assert.True(runtime.Get<ServiceRunGuard>().TryClaim(_tidy, out var scheduled));   // the scheduler's run is under way
        using var claim = scheduled;

        var started = await runtime.Start(A.RunServiceRequest().ForService(_tidy).Build(), Human());

        Expect.Equal(SindriErrors.JobConflict.Code, started.Error.Code);
        Assert.Contains("A scheduled run of tidy is in flight", started.Error.Message, StringComparison.Ordinal);
        Assert.Null(started.Error.Extensions);   // there is no running job to name
        Assert.Empty(runtime.Of(EventTypes.JobStarted));
    }

    [Fact(Timeout = 20_000)]
    public async Task A_second_manual_run_of_a_running_service_is_SND_JOB_409_naming_the_running_job()
    {
        var stuck = new BlockedService();
        await using var runtime = await RunServiceRuntime.Open(stuck);
        var first = await runtime.Start(A.RunServiceRequest().ForService(BlockedService.Name).Build(), Human());
        try
        {
            var second = await runtime.Start(A.RunServiceRequest().ForService(BlockedService.Name).Build(), Human());

            Expect.Equal(SindriErrors.JobConflict.Code, second.Error.Code);
            Expect.Equal(first.Value.JobId.Value, second.Error.Extensions!.First(static e => string.Equals(e.Key, "runningJob", StringComparison.Ordinal)).Value.GetString());   // why: the conflict names the running job
        }
        finally
        {
            stuck.Release();
        }

        await runtime.Quiesce().WaitAsync(TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = 20_000)]
    public async Task A_cancelled_manual_run_writes_no_service_ran()
    {
        var stuck = new BlockedService();
        await using var runtime = await RunServiceRuntime.Open(stuck);
        var started = await runtime.Start(A.RunServiceRequest().ForService(BlockedService.Name).Build(), Human());
        await stuck.Entered.Task.WaitAsync(TestContext.Current.CancellationToken);

        _ = await runtime.Cancel(started.Value.JobId);
        await runtime.Quiesce();

        Assert.Empty(runtime.Of(EventTypes.ServiceRan));
        Assert.Empty(runtime.Of(EventTypes.JobFinished));
        Assert.True(runtime.Get<ServiceRunGuard>().TryClaim(BlockedService.Name, out var free));   // the claim went with the run
        free.Dispose();
    }

    [Fact]
    public async Task A_run_that_returns_while_its_cancel_is_being_recorded_writes_no_service_ran()
    {
        var stuck = new BlockedService();
        await using var runtime = await RunServiceRuntime.Open(stuck);
        var started = await runtime.Start(A.RunServiceRequest().ForService(BlockedService.Name).Build(), Human());
        await stuck.Entered.Task.WaitAsync(Token);
        var appending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var proceed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        runtime.H.Store.BeforeAppend = entry =>
        {
            if (entry.Envelope.Type == EventTypes.JobCancelled)
            {
                _ = appending.TrySetResult();
                proceed.Task.Wait(Token);   // the cancel is mid-append: its token has not been tripped yet
            }
        };
        var cancel = Task.Run(() => runtime.Cancel(started.Value.JobId), Token);
        await appending.Task.WaitAsync(Token);

        stuck.Release();   // the service returns now, past the point a tripped token would have stopped it
        WorkReturned.Await(stuck.Returned.Task);   // and the run is parked in CancelRecorded before the cancel is allowed to settle: with the token tripped first it would throw at its first check instead
        _ = proceed.TrySetResult();   // the run waits for the cancel to settle before it decides, so the record goes through first
        _ = await cancel;
        await runtime.Quiesce().WaitAsync(TimeSpan.FromSeconds(10), TimeProvider.System, Token);

        Assert.Empty(runtime.Of(EventTypes.ServiceRan));
        Assert.Empty(runtime.Of(EventTypes.JobFinished));
    }

    [Fact]
    public async Task A_run_that_returns_while_its_cancel_fails_to_record_writes_its_service_ran_and_its_finish()
    {
        var stuck = new BlockedService();
        await using var runtime = await RunServiceRuntime.Open(stuck);
        var started = await runtime.Start(A.RunServiceRequest().ForService(BlockedService.Name).Build(), Human());
        await stuck.Entered.Task.WaitAsync(Token);
        runtime.H.Store.FailAppend = entry =>
        {
            if (entry.Envelope.Type != EventTypes.JobCancelled)
            {
                return null;
            }

            stuck.Release();   // the service returns while the cancel is being recorded, then the record fails
            return SindriErrors.LedgerUnavailable.With("busy");
        };

        var cancelled = await runtime.Cancel(started.Value.JobId);
        await runtime.Quiesce().WaitAsync(TimeSpan.FromSeconds(10), TimeProvider.System, Token);

        Assert.True(cancelled.IsError);
        _ = Assert.Single(runtime.Of(EventTypes.ServiceRan));
        _ = Assert.Single(runtime.Of(EventTypes.JobFinished));
        Assert.Empty(runtime.Of(EventTypes.JobCancelled));
    }

    [Fact]
    public async Task A_start_the_ledger_refuses_gives_the_service_back_to_the_scheduler()
    {
        await using var runtime = await RunServiceRuntime.Open(new ScriptedService(ScriptedService.Worked));
        runtime.H.Store.AppendFailure = new InvalidOperationException("the disk went away");

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.Start(A.RunServiceRequest().ForService(_tidy).Build(), Human()));

        Assert.True(runtime.Get<ServiceRunGuard>().TryClaim(_tidy, out var free));
        free.Dispose();
    }

    [Fact]
    public async Task Manual_ok_after_failure_clears_the_needs_you_row_and_sets_lastRun_ok()
    {
        await using var runtime = await RunServiceRuntime.Open(new ScriptedService(ScriptedService.Failed, ScriptedService.Worked));
        var request = A.RunServiceRequest().ForService(_tidy).Build();
        _ = await runtime.Start(request, Human());
        await runtime.Quiesce();
        _ = await runtime.Start(request, Human());
        await runtime.Quiesce();
        var views = new InMemoryViewStore(SliceHarness.Json);
        _ = await views.Open(ViewSchema.Version, Token);
        var rowsAfterEach = new List<int>();

        foreach (var recorded in runtime.Of(EventTypes.ServiceRan))
        {
            _ = await views.Apply([recorded], [new NeedsYouProjection(runtime.H.Events, new PassThroughScrubber(), []), new ServicesProjection(runtime.H.Events)], runtime.H.Clock.GetUtcNow(), Token);
            rowsAfterEach.Add((await views.List<NeedsYouRowRecord>(ViewNames.NeedsYou, Token)).Value.Count);
        }

        Expect.Sequence([1, 0], rowsAfterEach);
        var service = Assert.Single((await views.List<ServiceRow>(ViewNames.Services, Token)).Value);
        Expect.Equal(ServiceOutcome.Ok, service.LastRun!.Outcome);
    }
}
