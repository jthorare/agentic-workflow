using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.Jobs;
using Aw.Sindri.Features.ResumeJob;
using Aw.Sindri.Host.Catalog;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ResumeJob;

/// <summary>ResumeJob (model routing spec §5.3; desk): wakes a job parked on 'model-limit', answers the job as a start does, and refuses anything else with the not-waiting error.</summary>
[Lane(TestLane.Unit)]
public sealed class ResumeJobTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly JobId _job = A.FirstJobId;

    private readonly SliceHarness _h = new();
    private readonly JobWaits _waits;

    public ResumeJobTests()
    {
        _waits = new JobWaits(_h.Clock);
    }

    private ResumeJobService Service() => new(_waits, _h.Store, _h.Callers);

    private IDisposable Human() => _h.Callers.Enter(A.Caller().Human().Build());

    [Fact]
    public async Task ResumeJob_wakes_a_job_waiting_on_model_limit_and_answers_the_job()
    {
        var wait = _waits.Park(_job, "model-limit");
        using var entered = Human();

        var resumed = await Service().Handle(new ResumeJobRequest(_job), Token);

        Assert.True(resumed.IsSuccess, resumed.IsError ? resumed.Error.ToString() : null);
        Assert.True(wait.Came.Task.IsCompletedSuccessfully);
        Assert.False(_waits.IsWaiting(_job, "model-limit"));
        Assert.Equal(_job, resumed.Value.JobId);
        Assert.Equal($"/sindri/jobs/{_job.Value}", resumed.Value.Links.Job);
        Assert.Equal($"/sindri/jobs/{_job.Value}/cancel", resumed.Value.Links.Cancel);
    }

    [Fact]
    public async Task A_job_that_is_not_waiting_gets_the_not_waiting_refusal()
    {
        using var entered = Human();

        var refused = await Service().Handle(new ResumeJobRequest(_job), Token);

        Assert.Equal(SindriErrors.JobNotWaiting.Code, refused.Error.Code);
    }

    [Fact]
    public async Task A_job_waiting_on_another_signal_gets_the_same_refusal_and_keeps_waiting()
    {
        var wait = _waits.Park(_job, "index-login");
        using var entered = Human();

        var refused = await Service().Handle(new ResumeJobRequest(_job), Token);

        Assert.Equal(SindriErrors.JobNotWaiting.Code, refused.Error.Code);
        Assert.False(wait.Came.Task.IsCompleted);
        Assert.True(_waits.IsWaiting(_job, "index-login"));
    }

    [Fact]
    public async Task A_second_resume_of_the_same_wait_is_refused()
    {
        _ = _waits.Park(_job, "model-limit");
        using var entered = Human();
        _ = await Service().Handle(new ResumeJobRequest(_job), Token);

        var again = await Service().Handle(new ResumeJobRequest(_job), Token);

        Assert.Equal(SindriErrors.JobNotWaiting.Code, again.Error.Code);
    }

    [Fact]
    public void A_session_Sindri_launched_is_refused_with_SND_AUTH_002()
    {
        var row = ServiceRows.All.Single(static r => string.Equals(r.Action.Value, "ResumeJob", StringComparison.Ordinal));

        var decision = Authorization.Decide(row.Class, A.Caller().Session().Launched().Build(), row);

        Assert.Equal(ActionClass.Desk, row.Class);
        Assert.Equal(SindriErrors.AuthRefused.Code, decision.Refusal?.Code);
    }

    [Fact]
    public void The_human_at_a_terminal_and_in_the_app_may_resume()
    {
        var row = ServiceRows.All.Single(static r => string.Equals(r.Action.Value, "ResumeJob", StringComparison.Ordinal));

        Assert.Null(Authorization.Decide(row.Class, A.Caller().Human().AtTerminal().Build(), row).Refusal);
        Assert.Null(Authorization.Decide(row.Class, A.Caller().Human().Build(), row).Refusal);
    }

    [Theory]
    [InlineData("resume JOB-00000000000000000000000000000001", "resume", true)]
    [InlineData("resume JOB-00000000000000000000000000000001 --no-start", "resume", true)]
    [InlineData("resume", "", false)]
    [InlineData("resume a b", "", false)]
    public void The_jobs_command_line_reads_resume(string line, string verb, bool accepted)
    {
        var parsed = JobCommandLine.Parse(line.Split(' '));

        Assert.Equal(accepted, parsed.IsSuccess);
        if (accepted)
        {
            Assert.Equal(verb, parsed.Value.Verb);
            Assert.Equal("JOB-00000000000000000000000000000001", parsed.Value.Job);
        }
    }
}
