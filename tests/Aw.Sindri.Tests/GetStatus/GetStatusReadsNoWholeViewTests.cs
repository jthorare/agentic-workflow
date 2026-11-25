using System.Globalization;
using Aw.CodeIndex.Core.Values;
using Aw.CodeIndex.Testing;
using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.CodeIndexReads;
using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Server;
using Aw.Sindri.Tests.Views;
using RepoName = Aw.CodeIndex.Core.Values.RepoName;

namespace Aw.Sindri.Tests.GetStatus;

/// <summary>
/// GET /status reads a number of rows bounded by the profile and the live process, never by ledger history (issue #2786): today
/// HealthServiceRows.IdleAgents lists every launch ever made to produce one count.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class GetStatusReadsNoWholeViewTests
{
    private const string SeedType = "test.seed";
    private static readonly string[] _wholeViews = [ViewNames.Launches, ViewNames.Jobs, ViewNames.Items];
    private static readonly CommitSha _head = CommitSha.From(new string('1', 40));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_status_call_lists_no_launches_jobs_or_items_view()
    {
        var run = await Status(launches: 2000);

        Assert.DoesNotContain(run.Calls, static c => string.Equals(c.Method, "List", StringComparison.Ordinal) && _wholeViews.Contains(c.View));
    }

    [Fact]
    public async Task The_rows_a_status_call_reads_do_not_grow_with_the_launches()
    {
        var small = await Status(launches: 20);
        var large = await Status(launches: 2000);

        Assert.Equal(small.Calls.Sum(static c => c.Rows), large.Calls.Sum(static c => c.Rows));
    }

    [Fact]
    public async Task The_idle_count_ignores_a_relaunched_step_and_stays_right()
    {
        var run = await Status(launches: 2000);

        var problem = Assert.Single(run.Response.Health!.Problems, static p => p.Code == "SND-ENGINE-005");
        Assert.Equal("2 agents idle, no report", problem.Clause);
    }

    private static async Task<StatusRun> Status(int launches)
    {
        var h = new SliceHarness();
        var store = new InMemoryViewStore(SliceHarness.Json);
        var calls = new List<ReadCall>();
        var now = h.Clock.GetUtcNow();
        await Seed(h, store, Launches(launches, now), Jobs(300));

        var repo = RepoName.From("demo");
        var index = new FakeCodeIndex().WithIndex(repo, _head);
        var heads = new FakeRepoHeads().With(repo, _head, onDefaultBranch: true);
        var history = new InMemoryJobViews();
        foreach (var job in Jobs(300))
        {
            history.Put(job);
        }

        var jobs = new CountingJobViews(history, calls);

        h.Profiles.Live = Approved();
        var rows = new HealthServiceRows(new CountingViewReader(store, calls), [], new CodeIndexHealth(index, new RepoHealths(heads, jobs, new NoPauses(), h.Clock, new QueuedClock(h.Clock.GetUtcNow())), h.Profiles), clock: h.Clock);
        var service = new GetStatusService(h.Held, new ProfileReads(h.Profiles, h.Profiles), h.Store, new ProjectorProgress(h.Clock), rows, new InMemorySessionSnapshot());

        calls.Clear();   // seeding is not the status call
        var response = (await service.Handle(new GetStatusRequest(), Token)).Value;
        return new StatusRun(response, calls);
    }

    private static LoadedProfile Approved()
    {
        var profile = new SindriProfile(1, "thor", Repos: [new RepoEntry("demo", "/work/demo")]);
        return new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml");
    }

    private static string Launch(int n) => "LCH-" + n.ToString("x32", CultureInfo.InvariantCulture);

    private static string Instance(int n) => "ITM-" + n.ToString("x32", CultureInfo.InvariantCulture) + "/spec/1";

    /// <summary>Three launches ended idle for a Step inside the window; the third Step was relaunched since. The rest ended with an exit.</summary>
    private static List<LaunchRow> Launches(int total, DateTimeOffset now)
    {
        var rows = new List<LaunchRow>();
        for (var i = 0; i < total; i++)
        {
            var idle = i < 3;
            var end = new LaunchEnd(idle ? SessionEndReason.Idle : SessionEndReason.Exit, ExitCode: idle ? null : 0, now.AddHours(-1));
            rows.Add(Row(i, idle ? new LaunchStep(InstanceAddress.From(Instance(i)), "build", 1) : LaunchStep.None, now.AddHours(-2), end));
        }

        rows.Add(Row(total, new LaunchStep(InstanceAddress.From(Instance(2)), "build", 2), now.AddMinutes(-30), end: null));   // the engine's retry of the third idle Step
        return rows;
    }

    private static LaunchRow Row(int n, LaunchStep step, DateTimeOffset requestedAt, LaunchEnd? end) =>
        new(LaunchId.From(Launch(n)), Testing.A.FirstItemId, Repo: null, LaunchMode.Open, AgentId.From("claude-code"), "engine", LaunchState.Started, requestedAt, OutcomeAt: requestedAt,
            Code: null, Message: null, Stage: null, SessionId: null, ReportedSessionId: null, TabTitle: null, Viewer: null, Late: false, Attach: null, CatalogSource: null, ArgvHash: null,
            end, step, Sequence: n + 1L);

    private static List<JobRow> Jobs(int count) =>
        [.. Enumerable.Range(1, count).Select(static n => new JobBuilder()
            .WithId(JobId.From("JOB-" + n.ToString("x32", CultureInfo.InvariantCulture)))
            .OfKind(JobKind.From("BuildCodeIndex"), JobKey.From("index:demo"))
            .Ended(JobState.Succeeded)
            .Build())];

    /// <summary>Puts the rows into the views store the way a fold would: one event, one projection that writes them.</summary>
    private static async Task Seed(SliceHarness h, InMemoryViewStore store, List<LaunchRow> launches, List<JobRow> jobs)
    {
        _ = (await store.Open(ViewSchema.Version, Token)).Value;
        var seed = Recorded.Of(h.Events, SeedType, new ServiceRan(h.Ids.New<CommandId>(), ServiceName.From("heartbeat"), h.Clock.GetUtcNow(), ServiceOutcome.Ok, Code: null), sequence: 1);
        _ = (await store.Apply([seed], [new SeedProjection(launches, jobs)], h.Clock.GetUtcNow(), Token)).Value;
    }

    private sealed record ReadCall(string View, string Method, int Rows);

    private sealed record StatusRun(GetStatusResponse Response, List<ReadCall> Calls);

    private sealed class SeedProjection(List<LaunchRow> launches, List<JobRow> jobs) : IProjection
    {
        public IReadOnlyList<string> Views => [ViewNames.Launches, ViewNames.Jobs];

        public bool Handles(string eventType) => eventType == SeedType;

        public async ValueTask<Result<Unit>> Apply(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
        {
            foreach (var launch in launches)
            {
                _ = await rows.Put(ViewNames.Launches, launch.Id.Value, launch, cancellationToken);
            }

            foreach (var job in jobs)
            {
                _ = await rows.Put(ViewNames.Jobs, job.Id.Value, job, cancellationToken);
            }

            return Unit.Value;
        }
    }

    /// <summary>The real reader, counting every call and the rows it returns.</summary>
    private sealed class CountingViewReader(IViewReader inner, List<ReadCall> calls) : IViewReader
    {
        public async ValueTask<Result<TRow?>> Get<TRow>(string view, string key, CancellationToken cancellationToken)
            where TRow : class
        {
            var result = await inner.Get<TRow>(view, key, cancellationToken);
            calls.Add(new ReadCall(view, "Get", result.IsSuccess && result.Value is not null ? 1 : 0));
            return result;
        }

        public async ValueTask<Result<IReadOnlyList<TRow>>> List<TRow>(string view, CancellationToken cancellationToken)
            where TRow : class => Count(view, "List", await inner.List<TRow>(view, cancellationToken));

        public async ValueTask<Result<IReadOnlyList<TRow>>> Range<TRow>(string view, string afterKey, string? beforeKey, int max, CancellationToken cancellationToken)
            where TRow : class => Count(view, "Range", await inner.Range<TRow>(view, afterKey, beforeKey, max, cancellationToken));

        public async ValueTask<Result<IReadOnlyList<TRow>>> Where<TRow>(string view, ViewColumn column, string value, CancellationToken cancellationToken)
            where TRow : class => Count(view, "Where", await inner.Where<TRow>(view, column, value, cancellationToken));

        public ValueTask<Result<string>> Mark(string view, CancellationToken cancellationToken) => inner.Mark(view, cancellationToken);

        public ValueTask<Result<ProjectorState?>> State(CancellationToken cancellationToken) => inner.State(cancellationToken);

        private Result<IReadOnlyList<TRow>> Count<TRow>(string view, string method, Result<IReadOnlyList<TRow>> result)
        {
            calls.Add(new ReadCall(view, method, result.IsSuccess ? result.Value.Count : 0));
            return result;
        }
    }

    /// <summary>The jobs view reader, counting its lists under the 'jobs' view.</summary>
    private sealed class CountingJobViews(IJobViews inner, List<ReadCall> calls) : IJobViews
    {
        public ValueTask<Result<JobRow?>> Get(JobId job, CancellationToken cancellationToken) => inner.Get(job, cancellationToken);

        public async ValueTask<Result<IReadOnlyList<JobRow>>> Running(CancellationToken cancellationToken) => Count("Running", await inner.Running(cancellationToken));

        public async ValueTask<Result<IReadOnlyList<JobRow>>> List(JobState? state, JobKind? kind, CancellationToken cancellationToken) =>
            Count("List", await inner.List(state, kind, cancellationToken));

        private Result<IReadOnlyList<JobRow>> Count(string method, Result<IReadOnlyList<JobRow>> result)
        {
            calls.Add(new ReadCall(ViewNames.Jobs, method, result.IsSuccess ? result.Value.Count : 0));
            return result;
        }
    }
}
