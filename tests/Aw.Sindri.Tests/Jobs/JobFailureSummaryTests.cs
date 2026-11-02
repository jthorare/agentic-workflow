using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Jobs;

/// <summary>A failed job's recorded summary (GitHub #1644): a long error keeps its end, where the cause is.</summary>
public sealed class JobFailureSummaryTests
{
    private static readonly TimeSpan _bound = TimeSpan.FromSeconds(10);   // a hang guard: the job ends at once in any run

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_jobs_summary_keeps_the_end_of_a_long_error_where_the_cause_is()
    {
        await using var runtime = new RealJobRuntime();
        await runtime.Open();
        _ = await runtime.Start(new ProbeJobRequest("sql"), A.Caller().Human().Build());

        await runtime.Quiesce().WaitAsync(_bound, TimeProvider.System, TestContext.Current.CancellationToken);

        var finished = runtime.H.Events.Payload<JobFinished>(Assert.Single(runtime.Events, e => e.Envelope.Type == EventTypes.JobFinished)).Value;
        Expect.Equal(JobOutcome.Failed, finished.Outcome);
        Assert.Contains("FOREIGN KEY constraint failed", finished.Summary, StringComparison.Ordinal);
        Assert.True(finished.Summary.Length <= JobLimits.LineChars, $"the summary is {finished.Summary.Length} chars");
    }
}
