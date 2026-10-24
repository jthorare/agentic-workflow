using System.Globalization;
using System.Text;
using Aw.Kernel.Testing;
using Aw.Redaction;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Infrastructure.Text;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Jobs;

namespace Aw.Sindri.Tests.Jobs;

/// <summary>What the real runtime records beyond the contract (API P6 unit C): the end event's code, summary and result, causation, scrubbing, the running cap and the job context.</summary>
[Lane(TestLane.Unit)]
public sealed class JobRuntimeTests
{
    private static CallerContext Human() => A.Caller().Human().Build();

    private static async Task<RealJobRuntime> Open(Core.Text.ITextScrubber? scrubber = null)
    {
        var runtime = new RealJobRuntime(scrubber);
        await runtime.Open();
        return runtime;
    }

    private static JobFinished Finished(RealJobRuntime runtime) =>
        runtime.H.Events.Payload<JobFinished>(Assert.Single(runtime.Events, e => string.Equals(e.Envelope.Type, EventTypes.JobFinished, StringComparison.Ordinal))).Value;

    [Fact]
    public async Task A_returned_error_is_the_jobs_code()
    {
        await using var runtime = await Open();

        _ = await runtime.Start(new ProbeJobRequest("fail"), Human());
        await runtime.Quiesce();

        var finished = Finished(runtime);
        Expect.Equal((JobOutcome.Failed, SindriErrors.ServiceFailed.Code, "it broke"), (finished.Outcome, finished.Code, finished.Summary));
        Assert.Null(finished.Result);
    }

    [Fact]
    public async Task A_throw_is_a_failed_job_with_the_boundary_code()
    {
        await using var runtime = await Open();

        _ = await runtime.Start(new ProbeJobRequest("throw"), Human());
        await runtime.Quiesce();

        var finished = Finished(runtime);
        Expect.Equal((JobOutcome.Failed, SindriErrors.ServiceFailed.Code), (finished.Outcome, finished.Code));
        Assert.Contains("kaboom", finished.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_success_keeps_its_summary_and_result()
    {
        await using var runtime = await Open();

        _ = await runtime.Start(new EchoJobRequest(Lines: 2, DelayMs: 0), Human());
        await runtime.Quiesce();

        var finished = Finished(runtime);
        Expect.Equal((JobOutcome.Succeeded, "echoed 2"), (finished.Outcome, finished.Summary));
        Expect.Equal(2, finished.Result!.Value.GetProperty("lines").GetInt32());
    }

    [Fact]
    public async Task A_result_over_16_KiB_is_dropped_with_a_note()
    {
        await using var runtime = await Open();

        _ = await runtime.Start(new ProbeJobRequest("big"), Human());
        await runtime.Quiesce();

        var finished = Finished(runtime);
        Assert.Null(finished.Result);
        Assert.EndsWith("(result too large)", finished.Summary, StringComparison.Ordinal);
        Assert.True(Encoding.UTF8.GetByteCount(finished.Summary) < JobLimits.ResultBytes);
    }

    [Fact]
    public async Task Causation_of_job_events_is_the_job_started_event()
    {
        await using var runtime = await Open();

        _ = await runtime.Start(new EchoJobRequest(Lines: 2, DelayMs: 0), Human());
        await runtime.Quiesce();

        var started = Assert.Single(runtime.Events, e => string.Equals(e.Envelope.Type, EventTypes.JobStarted, StringComparison.Ordinal));
        var later = runtime.Events.Where(e => string.Equals(e.Envelope.Type, EventTypes.JobProgress, StringComparison.Ordinal) || string.Equals(e.Envelope.Type, EventTypes.JobFinished, StringComparison.Ordinal)).ToList();
        Expect.Equal(3, later.Count);
        Assert.All(later, e => Expect.Equal(started.Envelope.Id, e.Envelope.CausationId));
        Expect.Equal(started.Envelope.CorrelationId, later[0].Envelope.CorrelationId);
    }

    [Fact]
    public async Task A_line_is_scrubbed_escaped_and_cut_by_the_writer_before_the_event_is_written()
    {
        await using var runtime = await Open(new RedactingScrubber(Redactor.Default));

        _ = await runtime.Start(new ProbeJobRequest("secret"), Human());
        await runtime.Quiesce();

        var line = runtime.H.Events.Payload<JobProgress>(Assert.Single(runtime.Events, e => string.Equals(e.Envelope.Type, EventTypes.JobProgress, StringComparison.Ordinal))).Value.Line;
        Assert.DoesNotContain("ghp_", line, StringComparison.Ordinal);   // scrubbed
        Assert.DoesNotContain('\u001b', line);   // escaped
        Assert.DoesNotContain('\u0007', line);
        Assert.StartsWith("\\x1B]8;;x\\x07", line, StringComparison.Ordinal);
        Expect.Equal(JobLimits.LineChars, line.Length);   // cut to 200
    }

    [Fact]
    public async Task A_secret_in_a_result_string_is_scrubbed_where_it_is_stored()
    {
        await using var runtime = await Open(new RedactingScrubber(Redactor.Default));

        _ = await runtime.Start(new ProbeJobRequest("keyed"), Human());
        await runtime.Quiesce();

        var finished = Finished(runtime);
        Assert.DoesNotContain("ghp_", finished.Result!.Value.GetProperty("text").GetString(), StringComparison.Ordinal);
        Expect.Equal(JobOutcome.Succeeded, finished.Outcome);
    }

    [Fact]
    public async Task A_control_sequence_in_a_result_string_is_escaped_where_it_is_stored()
    {
        await using var runtime = await Open();

        _ = await runtime.Start(new ProbeJobRequest("esc"), Human());
        await runtime.Quiesce();

        var text = Finished(runtime).Result!.Value.GetProperty("text").GetString();
        Expect.Equal("\\x1B]0;title\\x07red\\x1B[31m", text);
    }

    [Fact]
    public async Task The_65th_running_job_is_refused_SND_JOB_002()
    {
        await using var runtime = await Open();
        var started = new List<Core.Ids.JobId>();
        for (var n = 1; n <= JobLimits.MaxRunning; n++)
        {
            started.Add((await runtime.Start(new ProbeJobRequest(string.Create(CultureInfo.InvariantCulture, $"hold{n}")), Human())).Value.JobId);
        }

        var over = await runtime.Start(new ProbeJobRequest("hold-over"), Human());

        Expect.Equal(SindriErrors.JobsRateLimited.Code, over.Error.Code);
        foreach (var job in started)
        {
            _ = await runtime.Cancel(job);
        }

        await runtime.Quiesce();
    }

    [Fact]
    public async Task Two_jobs_at_once_each_see_their_own_job_context()
    {
        await using var runtime = await Open();
        var a = await runtime.Start(new ProbeJobRequest("job"), Human());
        var b = await runtime.Start(new EchoJobRequest(Lines: 1, DelayMs: 0), Human());
        await runtime.Quiesce();

        var ids = runtime.Events.Where(e => e.Envelope.Type == EventTypes.JobFinished).Select(e => runtime.H.Events.Payload<JobFinished>(e).Value).ToList();
        Expect.Equal(2, ids.Count);
        var probe = ids.Single(f => f.Job == a.Value.JobId);
        Expect.Equal(a.Value.JobId.Value, probe.Result!.Value.GetProperty("text").GetString());
        Assert.Contains(ids, f => f.Job == b.Value.JobId);
    }

    [Fact]
    public async Task A_job_that_ends_frees_its_key_for_the_next_start()
    {
        await using var runtime = await Open();
        _ = await runtime.Start(new EchoJobRequest(Lines: 1, DelayMs: 0), Human());
        await runtime.Quiesce();

        var again = await runtime.Start(new EchoJobRequest(Lines: 1, DelayMs: 0), Human());

        Assert.True(again.IsSuccess);
        await runtime.Quiesce();
    }

    [Fact]
    public async Task The_start_answers_the_job_id_its_links_and_the_sequence_of_job_started()
    {
        await using var runtime = await Open();

        var started = await runtime.Start(new EchoJobRequest(Lines: 0, DelayMs: 0), Human());
        await runtime.Quiesce();

        var id = started.Value.JobId.Value;
        Expect.Equal(($"/sindri/jobs/{id}", $"/sindri/jobs/{id}/cancel"), (started.Value.Links.Job, started.Value.Links.Cancel));
        Assert.StartsWith($"/sindri/events?after={(started.Value.Accepted.Sequence - 1).ToString(CultureInfo.InvariantCulture)}&types=job.", started.Value.Links.Events, StringComparison.Ordinal);
        Expect.Equal(EventTypes.JobStarted, runtime.Events.Single(e => e.Sequence == started.Value.Accepted.Sequence).Envelope.Type);
    }

    [Fact]
    public async Task A_cancelled_job_holds_its_claim_until_its_work_has_returned()
    {
        await using var runtime = await Open();
        var first = await runtime.Start(new ProbeJobRequest("stubborn"), Human());
        try
        {
            await runtime.Latch.Entered.Task.WaitAsync(TestContext.Current.CancellationToken);
            _ = await runtime.Cancel(first.Value.JobId);

            var during = await runtime.Start(new ProbeJobRequest("stubborn"), Human());   // the old work is still winding down

            Assert.True(during.IsError);
            Expect.Equal(SindriErrors.JobConflict.Code, during.Error.Code);
            Expect.Equal(first.Value.JobId.Value, during.Error.Extensions!.First(static e => string.Equals(e.Key, "runningJob", StringComparison.Ordinal)).Value.GetString());
        }
        finally
        {
            _ = runtime.Latch.Release.TrySetResult();   // the stubborn work returns now
        }

        await runtime.Quiesce();
        Assert.True((await runtime.Start(new ProbeJobRequest("stubborn"), Human())).IsSuccess);
        await runtime.Quiesce();
    }
}
