using Aw.Kernel.Testing;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.ListJobs;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Views;

namespace Aw.Sindri.Tests.ListJobs;

/// <summary>GET /jobs?state=&amp;kind= (API P6 unit E, provisional owner default): newest first, filtered, session-scoped, with an empty sentence.</summary>
[Lane(TestLane.Unit)]
public sealed class ListJobsTests
{
    private static readonly JobKind _run = JobKind.From("RunService");
    private static readonly JobKind _sync = JobKind.From("SyncSources");
    private readonly ViewsHarness _v = new();
    private readonly InMemoryJobViews _jobs = new();
    private long _n;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private ListJobsService Service() => new(_jobs, _v.Progress, _v.Slice.Callers, new FixedJobKinds(_run, _sync));

    private IDisposable As(CallerContext caller) => _v.Slice.Callers.Enter(caller);

    private JobId Put(JobKind kind, JobState state, string? repo = null)
    {
        var id = JobId.From("JOB-" + (++_n).ToString("x32", System.Globalization.CultureInfo.InvariantCulture));
        var row = A.Job().WithId(id).OfKind(kind, JobKey.From("k" + _n.ToString(System.Globalization.CultureInfo.InvariantCulture))).InRepo(repo is null ? null : RepoName.From(repo));
        var built = (state == JobState.Running ? row : row.Ended(state)).Build();
        _jobs.Put(built with { Sequence = _n, StartedAt = built.StartedAt.AddMinutes(_n) });
        return id;
    }

    [Fact]
    public async Task ListJobs_lists_jobs_newest_first_filtered_by_state_and_kind()
    {
        var a = Put(_run, JobState.Succeeded);
        var b = Put(_sync, JobState.Running);
        var c = Put(_run, JobState.Running);
        using var entered = As(A.Caller().Human().Build());

        var all = (await Service().Handle(A.ListJobsRequest().Build(), Token)).Value;
        var running = (await Service().Handle(new ListJobsRequest(JobState.Running, Kind: null), Token)).Value;
        var runs = (await Service().Handle(new ListJobsRequest(State: null, _run), Token)).Value;

        Expect.Sequence([c, b, a], all.Jobs.Select(j => j.Id));
        Expect.Sequence([c, b], running.Jobs.Select(j => j.Id));
        Expect.Sequence([c, a], runs.Jobs.Select(j => j.Id));
        Assert.Null(all.Empty);
        Expect.Equal(_v.Progress.Current.AsOf, all.AsOf);
    }

    [Fact]
    public async Task A_session_lists_every_repos_jobs()
    {
        var mine = Put(_run, JobState.Running, "alpha");
        var repoLess = Put(_run, JobState.Running);
        var theirs = Put(_run, JobState.Running, "beta");
        using var entered = As(A.Caller().Session().InRepo("alpha").Build());

        var list = (await Service().Handle(A.ListJobsRequest().Build(), Token)).Value;

        Expect.Equal(3, list.Jobs.Count);
        Assert.Contains(list.Jobs, j => j.Id == theirs);
        Assert.Contains(list.Jobs, j => j.Id == mine);
        Assert.Contains(list.Jobs, j => j.Id == repoLess);
    }

    [Fact]
    public async Task No_jobs_answers_the_empty_sentence()
    {
        using var entered = As(A.Caller().Human().Build());

        var list = (await Service().Handle(A.ListJobsRequest().Build(), Token)).Value;

        Assert.Empty(list.Jobs);
        Expect.Equal(EmptyText.Jobs(state: null, kind: null), list.Empty);
    }

    [Fact]
    public async Task A_filter_with_no_match_names_its_filter_in_the_empty_sentence()
    {
        _ = Put(_run, JobState.Succeeded);
        using var entered = As(A.Caller().Human().Build());

        var failed = (await Service().Handle(new ListJobsRequest(JobState.Failed, Kind: null), Token)).Value;
        var sync = (await Service().Handle(new ListJobsRequest(State: null, _sync), Token)).Value;
        var both = (await Service().Handle(new ListJobsRequest(JobState.Running, _run), Token)).Value;

        Expect.Equal(("No failed jobs.", "No SyncSources jobs.", "No running RunService jobs."), (failed.Empty, sync.Empty, both.Empty));
    }

    [Fact]
    public async Task A_kind_no_job_registers_is_refused_before_any_read_and_names_the_kinds()
    {
        using var entered = As(A.Caller().Human().Build());

        var refused = await Service().Handle(new ListJobsRequest(State: null, JobKind.From("NoSuchJob")), Token);

        Expect.True(refused.IsError && string.Equals(refused.Error.Code, "SND-CLI-003", StringComparison.Ordinal));
        Assert.Contains("'NoSuchJob' isn't a job kind (fix: RunService, SyncSources)", refused.Error.Message, StringComparison.Ordinal);
    }
}
