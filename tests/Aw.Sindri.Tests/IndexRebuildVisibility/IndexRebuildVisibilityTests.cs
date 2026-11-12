using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Health;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.GetQueue;
using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Features.ListJobs;
using Aw.Sindri.Features.ListServices;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Commands;
using Aw.Sindri.Tests.Views;

namespace Aw.Sindri.Tests.IndexRebuildVisibility;

/// <summary>Issue #2945: a code index build or embed pass runs as a job, and status, services and queue show it.</summary>
public sealed class IndexRebuildVisibilityTests : IDisposable
{
    private static readonly FakeControlPlaneProbe _probe = FakeControlPlaneProbe.For(new ControlEndpoint(new Uri("http://127.0.0.1:7410"), 42, 3), new Ping(42, 3));
    private static readonly AsOf _fresh = new(5, 5, KnownInstants.Epoch, Rebuilding: false, Stalled: false);
    private static readonly ControlEndpoint _endpoint = new(new Uri("http://127.0.0.1:7410"), 42, 3);
    private readonly ViewsHarness _v = new();
    private readonly InMemoryJobViews _jobs = new();
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private SliceHarness H => _v.Slice;

    public void Dispose()
    {
        _out.Dispose();
        _error.Dispose();
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_lists_a_running_code_index_build_with_its_last_progress_line()
    {
        _jobs.Put(Build("index:web-app", "BuildCodeIndex", "service:code-index-main-move", "extracting 6700/14016 files"));

        var status = await Status();

        var job = Assert.Single(status.Jobs);
        Assert.Equal(("BuildCodeIndex", "web-app", "code-index-main-move", "extracting 6700/14016 files"), (job.Kind.Value, job.Repo!.Value.Value, job.Service, job.Progress));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_lists_a_running_embed_job()
    {
        _jobs.Put(Build("embed:web-app", "EmbedCodeIndex", "service:code-index-embed", "embeddings 85,434 of 95,968"));

        var status = await Status();

        var job = Assert.Single(status.Jobs);
        Assert.Equal(("EmbedCodeIndex", "code-index-embed"), (job.Kind.Value, job.Service));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_does_not_change_the_health_word_while_an_index_builds()
    {
        _jobs.Put(Build("index:web-app", "BuildCodeIndex", "service:code-index-main-move", "extracting 1/2 files"));

        var status = await Status();

        _ = Assert.Single(status.Jobs);   // the job is listed ...
        Assert.Equal(HealthWord.Healthy, status.Health!.Word);   // ... and building is not a health problem (spec 7.3)
    }

    [Fact, Trait("Lane", "unit")]
    public void Status_text_prints_one_jobs_line_naming_each_running_job()
    {
        var status = TextStatus(new RunningJobStatus(A.FirstJobId, JobKind.From("BuildCodeIndex"), RepoName.From("web-app"), "code-index-main-move", "extracting 6700/14016 files", KnownInstants.Epoch),
            new RunningJobStatus(JobId.From("JOB-00000000000000000000000000000002"), JobKind.From("EmbedCodeIndex"), RepoName.From("web-app"), "code-index-embed", "embeddings 85,434 of 95,968", KnownInstants.Epoch));

        var text = SummaryText.Render(status, _endpoint, ui: null).ReplaceLineEndings("\n");

        Assert.Contains("  jobs      2 running: BuildCodeIndex web-app (extracting 6700/14016 files) · EmbedCodeIndex web-app (embeddings 85,434 of 95,968)\n", text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Status_text_says_none_running_when_no_job_runs()
    {
        var text = SummaryText.Render(TextStatus(), _endpoint, ui: null).ReplaceLineEndings("\n");

        Assert.Contains("  jobs      none running\n", text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Services_shows_a_code_index_job_started_by_the_service_as_its_running_job()
    {
        _ = (await H.Profiles.Save(SliceHarness.ProfileOf("thor", [("code-index-main-move", "30s"), ("digest", "5m")]), Token)).Value;
        _jobs.Put(Build("index:web-app", "BuildCodeIndex", "service:code-index-main-move", "extracting 1/2 files"));   // Service is null: no admission names it

        var list = (await new ListServicesService(H.Profiles, _v.Views, _v.Progress, _jobs, [], new LiveSchedule(), H.Clock).Handle(new ListServicesRequest(), Token)).Value;

        Assert.Equal(A.FirstJobId, list.Services.Single(s => s.Name.Value == "code-index-main-move").Job);
        Assert.Null(list.Services.Single(s => s.Name.Value == "digest").Job);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Queue_text_says_jobs_run_when_no_item_runs()
    {
        var queue = new QueueResponse([new(QueueGroup.Running, [], EmptyText.Group(QueueGroup.Running))], Empty: null, _fresh);
        var running = new JobList([Summary("index:web-app", "BuildCodeIndex"), Summary("embed:web-app", "EmbedCodeIndex")], Empty: null, _fresh);
        var api = new RoutedApi().On("/sindri/queue", HttpStatusCode.OK, queue).On("/sindri/jobs", HttpStatusCode.OK, running);

        var code = await Verbs(api).Run(["queue"], Token);

        Assert.Equal(0, code);
        Assert.Contains("  2 job(s) running, not work items: sindri jobs --state running\n", _out.ToString().ReplaceLineEndings("\n"), StringComparison.Ordinal);
    }

    private async Task<GetStatusResponse> Status()
    {
        H.Profiles.Live = SliceHarness.Profile("5s");
        _ = await H.HoldLease();
        _v.Publish(stalledAt: null);
        var service = new GetStatusService(H.Held, new ProfileReads(H.Profiles, H.Profiles), H.Store, _v.Progress, new HealthServiceRows(_v.Views, [], jobs: _jobs), new InMemorySessionSnapshot());
        return (await service.Handle(new GetStatusRequest(), Token)).Value;
    }

    private static GetStatusResponse TextStatus(params RunningJobStatus[] jobs) =>
        new(Running: true, 42, 3, KnownInstants.Epoch, new ProfileStatus(Live: null, Problem: null, "/home/profile.yaml"), [])
        {
            Views = new ViewsStatus(Cursor: 9, LedgerHead: 9, KnownInstants.Epoch, Rebuilding: false, Stalled: false, StalledAt: null, StalledType: null),
            Health = new HealthReport(HealthWord.Healthy, [], Sentence: null),
            Jobs = jobs,
        };

    /// <summary>A running job as the scheduled service starts it: no Service on the row (nothing admits it), the service named only in StartedBy.</summary>
    private static JobRow Build(string key, string kind, string startedBy, string progress) =>
        new JobBuilder().OfKind(JobKind.From(kind), JobKey.From(key)).InRepo(RepoName.From("web-app")).Build() with
        {
            StartedBy = startedBy,
            Progress = [new JobLine(1, progress, KnownInstants.Epoch)],
            Rounds = 1,
        };

    private static JobSummary Summary(string key, string kind) =>
        new(A.FirstJobId, JobKind.From(kind), JobKey.From(key), Item: null, RepoName.From("web-app"), Service: null, JobState.Running, "service:code-index-main-move", KnownInstants.Epoch, EndedAt: null, Rounds: 1, Summary: null, Code: null);

    private ItemVerbs Verbs(RoutedApi api)
    {
        var client = new ControlPlaneClient(_probe, SliceHarness.Json,
            CallerHeaders.For(new AgentSession(Terminal: true, Prover: false), InMemoryHumanTokenStore.WithToken(), _ => ValueTask.FromResult((RepoName?)null)), handler: api);
        return new ItemVerbs(client, new Output(_out, _error, SliceHarness.Json), (_, _) => Task.FromResult<Kernel.Error?>(null));
    }
}
