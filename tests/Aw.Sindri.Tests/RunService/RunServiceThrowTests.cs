using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.RunService;

/// <summary>A background service that throws fails its run as SND-SCHED-001, and the message says what was thrown and by which method (#1769).</summary>
[Lane(TestLane.Unit)]
public sealed class RunServiceThrowTests
{
    [Fact]
    public async Task A_service_that_throws_fails_with_the_exception_type_and_the_throwing_method()
    {
        await using var runtime = await RunServiceRuntime.Open(new ThrowingService());

        _ = await runtime.Start(A.RunServiceRequest().ForService(ServiceName.From("tidy")).Build(), A.Caller().Human().Build());
        await runtime.Quiesce();

        var finished = runtime.H.Events.Payload<JobFinished>(Assert.Single(runtime.Of(EventTypes.JobFinished))).Value;
        Expect.Equal((JobOutcome.Failed, SindriErrors.ServiceFailed.Code), (finished.Outcome, finished.Code));
        Assert.Contains("boom", finished.Summary, StringComparison.Ordinal);
        Assert.Contains("(InvalidOperationException at ThrowingService.Run)", finished.Summary, StringComparison.Ordinal);
    }

    private sealed class ThrowingService : IScheduledService
    {
        public ServiceName Identity() => ServiceName.From("tidy");

        public ValueTask<Result<ScheduledResult>> Run(ScheduledRun run, CancellationToken cancellationToken) => throw new InvalidOperationException("boom");
    }
}
