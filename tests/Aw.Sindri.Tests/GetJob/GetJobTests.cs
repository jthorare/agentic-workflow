using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Features.GetJob;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Views;

namespace Aw.Sindri.Tests.GetJob;

/// <summary>GET /jobs/{jobId} (API P6 unit E): the job with its progress, actions and CLI while it runs; a session sees its own repo's and repo-less jobs only.</summary>
[Lane(TestLane.Unit)]
public sealed class GetJobTests
{
    private readonly ViewsHarness _v = new();
    private readonly InMemoryJobViews _jobs = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private GetJobService Service() => new(_jobs, _v.Progress, _v.Slice.Callers);

    private IDisposable As(CallerContext caller) => _v.Slice.Callers.Enter(caller);

    [Fact]
    public async Task GetJob_returns_the_job_with_its_actions_and_cli_while_running()
    {
        _jobs.Put(A.Job().WithProgress(3).Build());
        using var entered = As(A.Caller().Human().Build());

        var result = await Service().Handle(A.GetJobRequest().Build(), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        var job = result.Value;
        Expect.Equal((A.FirstJobId, JobState.Running, 3), (job.Id, job.State, job.Rounds));
        Expect.Sequence(["line 1", "line 2", "line 3"], job.Progress.Select(l => l.Line));
        Expect.Sequence([ActionId.From("CancelJob")], job.Actions);
        Expect.Equal($"sindri jobs cancel {A.FirstJobId.Value}", job.Cli);
        Expect.Equal(_v.Progress.Current.AsOf, job.AsOf);
    }

    [Fact]
    public async Task An_ended_job_has_no_actions_and_no_cli()
    {
        _jobs.Put(A.Job().Ended(JobState.Succeeded).Build());
        using var entered = As(A.Caller().Human().Build());

        var job = (await Service().Handle(A.GetJobRequest().Build(), Token)).Value;

        Assert.Empty(job.Actions);
        Assert.Null(job.Cli);
        Expect.Equal("succeeded", job.Summary);
    }

    [Fact]
    public async Task Get_unknown_is_SND_JOB_404()
    {
        using var entered = As(A.Caller().Human().Build());

        var result = await Service().Handle(A.GetJobRequest().Build(), Token);

        Expect.Equal(SindriErrors.JobNotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task Session_sees_every_repos_jobs()
    {
        var mine = JobId.From("JOB-0000000000000000000000000000000a");
        var repoLess = JobId.From("JOB-0000000000000000000000000000000b");
        var other = JobId.From("JOB-0000000000000000000000000000000c");
        _jobs.Put(A.Job().WithId(mine).InRepo(RepoName.From("alpha")).Build());
        _jobs.Put(A.Job().WithId(repoLess).Build());
        _jobs.Put(A.Job().WithId(other).InRepo(RepoName.From("beta")).Build());
        using var entered = As(A.Caller().Session().InRepo("alpha").Build());

        var seen = new[] { mine, repoLess, other }.Select(async id => (await Service().Handle(new GetJobRequest(id), Token)).IsSuccess);

        Expect.Sequence([true, true, true], await Task.WhenAll(seen));
    }

    [Fact]
    public async Task A_human_sees_every_repo()
    {
        _jobs.Put(A.Job().InRepo(RepoName.From("beta")).Build());
        using var entered = As(A.Caller().Human().Build());

        Assert.True((await Service().Handle(A.GetJobRequest().Build(), Token)).IsSuccess);
    }
}
