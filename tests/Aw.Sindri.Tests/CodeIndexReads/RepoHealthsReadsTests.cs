using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;
using Aw.CodeIndex.Testing;
using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.CodeIndexReads;
using Aw.Sindri.Testing;
using RepoName = Aw.CodeIndex.Core.Values.RepoName;

namespace Aw.Sindri.Tests.CodeIndexReads;

/// <summary>The build jobs a status call reads (issue #2786): the ended builds only when a repo is behind, once, and the words stay the same.</summary>
public sealed class RepoHealthsReadsTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);
    private static readonly RepoName _repo = RepoName.From("demo");
    private static readonly CommitSha _old = CommitSha.From(new string('1', 40));
    private static readonly CommitSha _new = CommitSha.From(new string('2', 40));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task An_up_to_date_repo_lists_no_jobs()
    {
        var (jobs, health) = await Read(live: _old, failedBuilds: 2);

        Assert.Equal(0, jobs.Lists);
        Assert.Equal("ok", health.State);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_repo_behind_with_two_failed_builds_still_says_retrying_and_lists_once()
    {
        var (jobs, health) = await Read(live: _new, failedBuilds: 2);

        Assert.Equal(1, jobs.Lists);
        Assert.Equal(("waiting", "retrying in 8 min"), (health.State, health.Waiting));   // 2 failures wait 10 min, the newest ended 2 min ago
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_repo_read_queued_twice_31_min_apart_has_been_queued_31_min()
    {
        var now = new MovingClock(_now);
        var healths = new RepoHealths(new FakeRepoHeads().With(_repo, _old, onDefaultBranch: true), new InMemoryJobViews(), now, new QueuedClock(_now));
        var missing = new RepoStatus(_repo, IndexState.Missing, Commit: null, BuiltAt: null, [], Counts: null);
        var scope = new CodeIndexScope([new CodeIndexRepo(_repo, "/work/demo", CodeIndexSettings.Default)], new CodeIndexCaller(CallerKind.Service, Repo: null));

        _ = await healths.Of(scope, [missing], Token);
        now.Now = _now.AddMinutes(31);
        var health = Assert.Single(await healths.Of(scope, [missing], Token));

        Assert.Equal(("queued", TimeSpan.FromMinutes(31)), (health.State, health.QueuedFor));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_repo_added_3_h_into_serve_and_queued_on_its_first_read_counts_from_that_read_3502()
    {
        var healths = new RepoHealths(new FakeRepoHeads().With(_repo, _old, onDefaultBranch: true), new InMemoryJobViews(), new MovingClock(_now.AddHours(3)), new QueuedClock(_now));
        var missing = new RepoStatus(_repo, IndexState.Missing, Commit: null, BuiltAt: null, [], Counts: null);
        var scope = new CodeIndexScope([new CodeIndexRepo(_repo, "/work/demo", CodeIndexSettings.Default)], new CodeIndexCaller(CallerKind.Service, Repo: null));

        var first = Assert.Single(await healths.Of(scope, [missing], Token));

        Assert.Equal(("queued", TimeSpan.Zero, false), (first.State, first.QueuedFor, first.NeedsLook));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_repo_queued_past_30_min_needs_a_look_and_one_under_does_not()
    {
        var now = new MovingClock(_now);
        var healths = new RepoHealths(new FakeRepoHeads().With(_repo, _old, onDefaultBranch: true), new InMemoryJobViews(), now, new QueuedClock(_now));
        var missing = new RepoStatus(_repo, IndexState.Missing, Commit: null, BuiltAt: null, [], Counts: null);
        var scope = new CodeIndexScope([new CodeIndexRepo(_repo, "/work/demo", CodeIndexSettings.Default)], new CodeIndexCaller(CallerKind.Service, Repo: null));

        var first = Assert.Single(await healths.Of(scope, [missing], Token));
        now.Now = _now.AddMinutes(30);
        var later = Assert.Single(await healths.Of(scope, [missing], Token));

        Assert.Equal((false, true), (first.NeedsLook, later.NeedsLook));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_running_read_neither_starts_nor_forgets_the_queued_count()
    {
        var now = new MovingClock(_now);
        var jobs = new FlakyRunningJobs(new InMemoryJobViews());
        var clock = new QueuedClock(_now);
        clock.Seed([_repo.Value]);   // there at serve start, so its count runs from serve start (#3480)
        var healths = new RepoHealths(new FakeRepoHeads().With(_repo, _old, onDefaultBranch: true), jobs, now, clock);
        var missing = new RepoStatus(_repo, IndexState.Missing, Commit: null, BuiltAt: null, [], Counts: null);
        var scope = new CodeIndexScope([new CodeIndexRepo(_repo, "/work/demo", CodeIndexSettings.Default)], new CodeIndexCaller(CallerKind.Service, Repo: null));

        now.Now = _now.AddMinutes(31);
        jobs.Fails = true;
        var blind = Assert.Single(await healths.Of(scope, [missing], Token));
        now.Now = _now.AddMinutes(40);
        jobs.Fails = false;
        var seen = Assert.Single(await healths.Of(scope, [missing], Token));

        Assert.Equal((null, false), (blind.QueuedFor, blind.NeedsLook));   // a failed read can't tell queued from building (#3481)
        Assert.Equal(TimeSpan.FromMinutes(40), seen.QueuedFor);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_repo_that_builds_and_goes_queued_again_counts_from_zero()
    {
        var now = new MovingClock(_now);
        var jobs = new InMemoryJobViews();
        var healths = new RepoHealths(new FakeRepoHeads().With(_repo, _old, onDefaultBranch: true), jobs, now, new QueuedClock(_now));
        var missing = new RepoStatus(_repo, IndexState.Missing, Commit: null, BuiltAt: null, [], Counts: null);
        var scope = new CodeIndexScope([new CodeIndexRepo(_repo, "/work/demo", CodeIndexSettings.Default)], new CodeIndexCaller(CallerKind.Service, Repo: null));
        var build = Testing.A.Job().WithId(JobId.From("JOB-" + new string('b', 32))).OfKind(JobKind.From("BuildCodeIndex"), JobKey.From("index:demo")).Build();

        _ = await healths.Of(scope, [missing], Token);
        now.Now = _now.AddMinutes(10);
        jobs.Put(build with { State = JobState.Running, StartedAt = now.Now });
        var building = Assert.Single(await healths.Of(scope, [missing], Token));
        now.Now = _now.AddMinutes(40);
        jobs.Put(build with { State = JobState.Failed, StartedAt = _now.AddMinutes(10), EndedAt = _now.AddMinutes(12) });
        var again = Assert.Single(await healths.Of(scope, [missing], Token));   // one failure's retry wait ended long before +40 min, so the repo reads queued again
        now.Now = _now.AddMinutes(45);
        var later = Assert.Single(await healths.Of(scope, [missing], Token));

        Assert.Equal("building", building.State);
        Assert.Null(building.QueuedFor);
        Assert.Equal(("queued", (TimeSpan?)TimeSpan.Zero), (again.State, again.QueuedFor));
        Assert.Equal(TimeSpan.FromMinutes(5), later.QueuedFor);
    }

    private static async Task<(CountingJobs Jobs, RepoHealth Health)> Read(CommitSha live, int failedBuilds)
    {
        var history = new InMemoryJobViews();
        for (var n = 1; n <= failedBuilds; n++)
        {
            var job = Testing.A.Job().WithId(JobId.From("JOB-" + n.ToString("x32", System.Globalization.CultureInfo.InvariantCulture))).OfKind(JobKind.From("BuildCodeIndex"), JobKey.From("index:demo")).Ended(JobState.Failed).Build();
            history.Put(job with { StartedAt = _now.AddMinutes((-10 * (failedBuilds - n)) - 3), EndedAt = _now.AddMinutes((-10 * (failedBuilds - n)) - 2) });
        }

        var jobs = new CountingJobs(history);
        var heads = new FakeRepoHeads().With(_repo, live, onDefaultBranch: true);
        var status = new RepoStatus(_repo, IndexState.Ok, _old, _now.AddHours(-1), [new LayerStatus(LayerName.Structure, LayerState.Ok, Code: null, "ok")], new IndexCounts(Files: 1, Symbols: 2, Edges: 3));
        var scope = new CodeIndexScope([new CodeIndexRepo(_repo, "/work/demo", CodeIndexSettings.Default)], new CodeIndexCaller(CallerKind.Service, Repo: null));

        var healths = await new RepoHealths(heads, jobs, new FixedClock(_now), new QueuedClock(_now)).Of(scope, [status], Token);
        return (jobs, Assert.Single(healths));
    }

    private sealed class MovingClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class CountingJobs(IJobViews inner) : IJobViews
    {
        public int Lists { get; private set; }

        public ValueTask<Result<JobRow?>> Get(JobId job, CancellationToken cancellationToken) => inner.Get(job, cancellationToken);

        public ValueTask<Result<IReadOnlyList<JobRow>>> Running(CancellationToken cancellationToken) => inner.Running(cancellationToken);

        public ValueTask<Result<IReadOnlyList<JobRow>>> List(JobState? state, JobKind? kind, CancellationToken cancellationToken)
        {
            Lists++;
            return inner.List(state, kind, cancellationToken);
        }
    }

    private sealed class FlakyRunningJobs(IJobViews inner) : IJobViews
    {
        public bool Fails { get; set; }

        public ValueTask<Result<JobRow?>> Get(JobId job, CancellationToken cancellationToken) => inner.Get(job, cancellationToken);

        public ValueTask<Result<IReadOnlyList<JobRow>>> Running(CancellationToken cancellationToken) =>
            Fails ? ValueTask.FromResult<Result<IReadOnlyList<JobRow>>>(new Error("views.unavailable", ErrorKind.Unavailable, "the views are down")) : inner.Running(cancellationToken);

        public ValueTask<Result<IReadOnlyList<JobRow>>> List(JobState? state, JobKind? kind, CancellationToken cancellationToken) => inner.List(state, kind, cancellationToken);
    }
}
