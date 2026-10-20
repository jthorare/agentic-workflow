using System.Net;
using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Host;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.ApiGoldens;
using Aw.Sindri.Tests.Callers;
using Aw.Sindri.Tests.Server;
using Microsoft.Extensions.DependencyInjection;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.Jobs;

/// <summary>
/// The job and service routes on the real host, a real SQLite ledger and the real runner (API P6 unit G): the happy paths, the error codes, the Origin
/// refusal and the session scope. The echo job is mapped through MapJob at a route only this test host has (S10).
/// </summary>
[Lane(TestLane.Integration)]
public sealed class JobEndpointTests
{
    private static JsonElement Json(Answer answer) => JsonDocument.Parse(answer.Body).RootElement.Clone();

    private static async Task<IReadOnlyList<LedgerEvent>> Ledger(GoldenHost host) =>
        (await host.App.Services.GetRequiredService<ILedgerReader>().ReadAfter(0, 1000, TestContext.Current.CancellationToken)).Value;

    private static Task<Answer> Post(GoldenHost host, string path, params (string Name, string Value)[] extra) =>
        host.Http.Ask(CallerRequests.Post(path, json: null, [.. JobHost.Human(host), .. extra]));

    private static async Task<(JobId Job, long Sequence)> Run(GoldenHost host, string service)
    {
        var started = await Post(host, $"/sindri/services/{service}/run");
        Assert.Equal(HttpStatusCode.OK, started.Status);
        return (JobId.From(Json(started).GetProperty("jobId").GetString()!), Json(started).GetProperty("accepted").GetProperty("sequence").GetInt64());   // why: a started job answers its id
    }

    [Fact(Timeout = 30_000)]
    public async Task EchoJob_runs_end_to_end_through_MapJob()
    {
        await using var host = await JobHost.Start(TestContext.Current.CancellationToken);

        var (job, _) = await JobHost.StartEcho(host, lines: 2, delayMs: 0);
        await JobHost.Settle(host, TestContext.Current.CancellationToken);
        var view = Json(await JobHost.Read(host, $"/sindri/jobs/{job.Value}"));

        Assert.Equal(("succeeded", "EchoJob", "echoed 2"), (view.GetProperty("state").GetString(), view.GetProperty("kind").GetString(), view.GetProperty("summary").GetString()));
        Assert.Equal(["echo 1", "echo 2"], view.GetProperty("progress").EnumerateArray().Select(static l => l.GetProperty("line").GetString()), StringComparer.Ordinal);
        Assert.Equal(2, view.GetProperty("result").GetProperty("lines").GetInt32());
        Assert.Equal(["job.started", "job.progress", "job.progress", "job.finished"], (await Ledger(host)).Select(static e => e.Envelope.Type).Where(static t => t.StartsWith("job.", StringComparison.Ordinal)), StringComparer.Ordinal);
    }

    [Fact(Timeout = 30_000)]
    public async Task RunService_runs_the_service_and_the_job_ends_succeeded()
    {
        await using var host = await JobHost.Start(TestContext.Current.CancellationToken);
        await ViewScenarios.Approve(host, TestContext.Current.CancellationToken);

        var (job, _) = await Run(host, "heartbeat");
        await JobHost.Settle(host, TestContext.Current.CancellationToken);
        var view = Json(await JobHost.Read(host, $"/sindri/jobs/{job.Value}"));

        Assert.Equal(("succeeded", "RunService", "heartbeat"), (view.GetProperty("state").GetString(), view.GetProperty("kind").GetString(), view.GetProperty("service").GetString()));
    }

    [Fact(Timeout = 30_000)]
    public async Task A_manual_run_of_a_paused_service_runs()
    {
        await using var host = await JobHost.Start(TestContext.Current.CancellationToken);
        await ViewScenarios.Approve(host, TestContext.Current.CancellationToken);
        var paused = await Post(host, "/sindri/services/heartbeat/pause");
        Assert.Equal(HttpStatusCode.OK, paused.Status);
        await JobHost.Settle(host, TestContext.Current.CancellationToken);

        var (job, _) = await Run(host, "heartbeat");   // a pause stops the schedule, never a person
        await JobHost.Settle(host, TestContext.Current.CancellationToken);
        var view = Json(await JobHost.Read(host, $"/sindri/jobs/{job.Value}"));

        Assert.Equal(("succeeded", "heartbeat"), (view.GetProperty("state").GetString(), view.GetProperty("service").GetString()));
    }

    [Fact(Timeout = 30_000)]
    public async Task A_second_run_of_a_running_service_is_409_SND_JOB_409_naming_the_running_job()
    {
        await using var host = await JobHost.Start(TestContext.Current.CancellationToken);
        await ViewScenarios.Approve(host, TestContext.Current.CancellationToken);
        var (first, _) = await Run(host, "stuck");

        var second = await Post(host, "/sindri/services/stuck/run");
        host.App.Services.GetRequiredService<BlockedService>().Release();
        await JobHost.Settle(host, TestContext.Current.CancellationToken);

        Assert.Equal((HttpStatusCode.Conflict, "SND-JOB-409"), (second.Status, second.Code));
        Assert.True(Json(second).TryGetProperty("runningJob", out var running), second.Body);
        Assert.Equal(first.Value, running.GetString());
    }

    [Fact(Timeout = 30_000)]
    public async Task Run_of_a_service_the_profile_does_not_name_is_404_SND_SVC_404()
    {
        await using var host = await JobHost.Start(TestContext.Current.CancellationToken);
        await ViewScenarios.Approve(host, TestContext.Current.CancellationToken);

        var answer = await Post(host, "/sindri/services/ghost/run");

        Assert.Equal((HttpStatusCode.NotFound, "SND-SVC-404"), (answer.Status, answer.Code));
    }

    [Fact(Timeout = 30_000)]
    public async Task A_built_in_service_is_listed_and_runs_by_name()
    {
        await using var host = await JobHost.Start(TestContext.Current.CancellationToken);
        await ViewScenarios.Approve(host, TestContext.Current.CancellationToken);
        await ViewScenarios.CatchUp(host, TestContext.Current.CancellationToken);
        var builtIn = host.App.Services.GetServices<BuiltInService>().First().Name.Value;   // the profile does not name it

        var list = Json(await JobHost.Read(host, "/sindri/services")).GetProperty("services").EnumerateArray().Select(static s => s.GetProperty("name").GetString());
        var run = await Post(host, $"/sindri/services/{builtIn}/run");

        Assert.Contains(builtIn, list, StringComparer.Ordinal);
        Assert.NotEqual(HttpStatusCode.NotFound, run.Status);
        Assert.NotEqual("SND-SVC-404", run.Code, StringComparer.Ordinal);
    }

    [Fact(Timeout = 30_000)]
    public async Task Cancel_ends_a_running_job_cancelled_then_a_second_cancel_is_409_and_an_unknown_job_404()
    {
        await using var host = await JobHost.Start(TestContext.Current.CancellationToken);
        var (job, sequence) = await JobHost.StartEcho(host, lines: 100, delayMs: 100);
        await ViewScenarios.CatchUp(host, TestContext.Current.CancellationToken);

        var cancelled = await Post(host, $"/sindri/jobs/{job.Value}/cancel");
        await JobHost.Settle(host, TestContext.Current.CancellationToken);
        var again = await Post(host, $"/sindri/jobs/{job.Value}/cancel");
        var unknown = await Post(host, "/sindri/jobs/JOB-000000000000000000000000000000ff/cancel");

        Assert.True(Json(cancelled).GetProperty("sequence").GetInt64() > sequence);
        Assert.Equal("cancelled", Json(await JobHost.Read(host, $"/sindri/jobs/{job.Value}")).GetProperty("state").GetString());
        Assert.Equal(((HttpStatusCode.Conflict, "SND-JOB-409"), (HttpStatusCode.NotFound, "SND-JOB-404")), ((again.Status, again.Code), (unknown.Status, unknown.Code)));
        Assert.DoesNotContain(await Ledger(host), static e => string.Equals(e.Envelope.Type, EventTypes.JobFinished, StringComparison.Ordinal));
    }

    [Fact(Timeout = 30_000)]
    public async Task Pause_and_resume_are_recorded_and_an_unknown_service_is_404()
    {
        await using var host = await JobHost.Start(TestContext.Current.CancellationToken);
        await ViewScenarios.Approve(host, TestContext.Current.CancellationToken);

        var paused = await Post(host, "/sindri/services/heartbeat/pause");
        var resumed = await Post(host, "/sindri/services/heartbeat/resume");
        var ghost = await Post(host, "/sindri/services/ghost/pause");

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK, "SND-SVC-404"), (paused.Status, resumed.Status, ghost.Code));
        Assert.Equal([EventTypes.ServicePaused, EventTypes.ServiceResumed],
            (await Ledger(host)).Select(static e => e.Envelope.Type).Where(static t => t.StartsWith("service.", StringComparison.Ordinal) && t != EventTypes.ServiceRan), StringComparer.Ordinal);
    }

    [Theory(Timeout = 30_000)]
    [InlineData("/sindri/services/heartbeat/run")]
    [InlineData("/sindri/services/heartbeat/pause")]
    [InlineData("/sindri/services/heartbeat/resume")]
    [InlineData("/sindri/jobs/JOB-00000000000000000000000000000001/cancel")]
    [InlineData(JobHost.EchoRoute)]
    public async Task A_command_with_an_Origin_header_is_403_SND_API_008_and_writes_nothing(string route)
    {
        await using var host = await JobHost.Start(TestContext.Current.CancellationToken);
        var before = (await Ledger(host)).Count;

        var answer = await Post(host, route, ("Origin", "http://localhost:3000"));

        Assert.Equal((HttpStatusCode.Forbidden, "SND-API-008"), (answer.Status, answer.Code));
        Assert.Equal(before, (await Ledger(host)).Count);
    }

    [Fact(Timeout = 30_000)]
    public async Task A_session_sees_a_job_of_its_own_repo_and_not_one_of_another()
    {
        await using var host = await JobHost.Start(TestContext.Current.CancellationToken);
        await ViewScenarios.Approve(host, TestContext.Current.CancellationToken);
        var started = new JobStarted(CommandId.From(new Guid(0xb1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)), A.FirstJobId, JobKind.From("EchoJob"), JobKey.From("echo"), Item: null, RepoName.From("demo"), Service: null);
        await ViewScenarios.Append(host, EventTypes.JobStarted, started, "job.started:repo", TestContext.Current.CancellationToken);
        await ViewScenarios.CatchUp(host, TestContext.Current.CancellationToken);
        var path = $"/sindri/jobs/{A.FirstJobId.Value}";

        var own = await host.Http.Ask(Get(path, (SindriHeaders.Caller, "session"), (SindriHeaders.Repo, "demo")));
        var other = await host.Http.Ask(Get(path, (SindriHeaders.Caller, "session"), (SindriHeaders.Repo, "other")));

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.NotFound, "SND-JOB-404"), (own.Status, other.Status, other.Code));
    }

    [Fact(Timeout = 60_000)]
    public async Task The_list_filters_by_state_and_kind()
    {
        await using var host = await JobHost.Start(TestContext.Current.CancellationToken);
        var (running, _) = await JobHost.StartEcho(host, lines: 100, delayMs: 100);
        await ViewScenarios.CatchUp(host, TestContext.Current.CancellationToken);

        var byState = Json(await JobHost.Read(host, "/sindri/jobs?state=running"));
        var byKind = Json(await JobHost.Read(host, "/sindri/jobs?kind=RunService"));
        var bad = await JobHost.Read(host, "/sindri/jobs?state=maybe");
        await JobHost.Cancel(host, running, TestContext.Current.CancellationToken);   // the job ends only through this cancel: a refused one fails here, by name
        await JobHost.Settle(host, TestContext.Current.CancellationToken);

        Assert.Equal([running.Value], byState.GetProperty("jobs").EnumerateArray().Select(static j => j.GetProperty("id").GetString()), StringComparer.Ordinal);
        Assert.Equal(0, byKind.GetProperty("jobs").GetArrayLength());
        Assert.Equal((HttpStatusCode.BadRequest, "SND-CLI-003"), (bad.Status, bad.Code));
    }

    [Fact(Timeout = 30_000)]
    public async Task A_filter_with_no_match_says_so_and_an_unknown_kind_or_state_is_refused_before_any_read()
    {
        await using var host = await JobHost.Start(TestContext.Current.CancellationToken);
        await ViewScenarios.CatchUp(host, TestContext.Current.CancellationToken);

        var none = Json(await JobHost.Read(host, "/sindri/jobs"));
        var failedRuns = Json(await JobHost.Read(host, "/sindri/jobs?state=failed&kind=RunService"));
        var running = Json(await JobHost.Read(host, "/sindri/jobs?state=running"));
        var unknownKind = await JobHost.Read(host, "/sindri/jobs?kind=NoSuchJob");
        var unknownState = await JobHost.Read(host, "/sindri/jobs?state=paused");

        Assert.StartsWith("No jobs have run yet", none.GetProperty("empty").GetString(), StringComparison.Ordinal);
        Assert.Equal("No failed RunService jobs.", failedRuns.GetProperty("empty").GetString());
        Assert.Equal("No running jobs.", running.GetProperty("empty").GetString());
        Assert.Equal((HttpStatusCode.BadRequest, "SND-CLI-003"), (unknownKind.Status, unknownKind.Code));
        Assert.Equal((HttpStatusCode.BadRequest, "SND-CLI-003"), (unknownState.Status, unknownState.Code));
        Assert.Contains("RunService", unknownKind.Body, StringComparison.Ordinal);
    }
}
