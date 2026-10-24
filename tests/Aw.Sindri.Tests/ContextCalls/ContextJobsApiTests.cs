using System.Net;
using System.Text.Json;
using Aw.Context.Core.Extract;
using Aw.Context.Testing.Extract;
using Aw.Context.Testing.Fakes;
using Aw.Context.Testing.Fixtures;
using Aw.Context.Testing.Map;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Host;
using Aw.Sindri.Infrastructure.Locks;
using Aw.Sindri.Tests.ApiGoldens;
using Aw.Sindri.Tests.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>
/// 'sources sync' and 'map build' as job routes on a started host (API P6 follow-up; API spec §4.1): the start answers JobStarted, the work runs on the
/// job runtime and leaves its events and a small result, a second start of the same job is SND-JOB-409, and a session is refused at the start.
/// </summary>
public sealed class ContextJobsApiTests : IDisposable
{
    private const string KeyVariable = "SINDRI_TEST_ONYX_KEY";
    private readonly FixtureRepo _repo = FixtureRepo.Build();
    private readonly string _locks = Directory.CreateTempSubdirectory("aw-ctx-p6-locks-").FullName;

    public void Dispose()
    {
        _repo.Dispose();
        Directory.Delete(_locks, recursive: true);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static JsonElement Json(string body) => JsonDocument.Parse(body).RootElement.Clone();

    private static async Task<IReadOnlyList<LedgerEvent>> Ledger(GoldenHost host) =>
        (await host.App.Services.GetRequiredService<ILedgerReader>().ReadAfter(0, 5000, Token)).Value;

    private static IEnumerable<LedgerEvent> Of(IEnumerable<LedgerEvent> events, string type) => events.Where(e => string.Equals(e.Envelope.Type, type, StringComparison.Ordinal));

    /// <summary>A host that maps the fixture map's repo and names Onyx, with the fixture map imported (the sync's input).</summary>
    private async Task<GoldenHost> SyncHost(FakeOnyxServer onyx, Action<IServiceCollection>? extra = null)
    {
        var yaml = $"schemaVersion: 1\nuser: golden\nrepos:\n  - {{ name: {FixtureMap.Repo}, path: \"{_repo.Path}\" }}\n"
            + "context:\n  docs: { globs: [\"docs/**/*.md\"] }\n  sources:\n"
            + $"    onyx: {{ url: \"{onyx.Url}\", token: \"env:{KeyVariable}\", version: \"{FakeOnyxServer.PinnedVersion}\" }}\n";
        var host = await GoldenHost.Start(temporary: true, Token, yaml, new Dictionary<string, string>(StringComparer.Ordinal) { [KeyVariable] = onyx.Token }, extra);
        var hash = (await host.LiveHash(Token)).Value;
        Assert.Equal(HttpStatusCode.OK, (await host.Http.Ask(Post("/sindri/profile/approve", await ApiScenarios.Confirmed(host, hash), Human(host.Token.Value)))).Status);
        await ContextMapApiTests.Import(host);
        return host;
    }

    /// <summary>A host over the fixture repo with fake clusters and a lock directory of the test's own (graphify is a heavy-lane test).</summary>
    private Task<GoldenHost> BuildHost() => ContextHosts.Approved(_repo, sessions: false, Token, services =>
    {
        _ = services.RemoveAll<IClusterSource>();
        _ = services.AddSingleton<IClusterSource>(new FakeClusterSource());
        _ = services.RemoveAll<HeavyLockSettings>();
        _ = services.AddSingleton(new HeavyLockSettings(Path.Combine(_locks, "heavy-job.lock"), 2, Held: false));
    });

    private static async Task<(string Job, JsonElement Body)> Start(GoldenHost host, string route, string body)
    {
        var started = await host.Http.Ask(Post(route, body, Human(host.Token.Value)));
        Assert.True(started.Status == HttpStatusCode.OK, started.Body);
        var json = Json(started.Body);
        return (json.GetProperty("jobId").GetString()!, json);
    }

    [Fact(Timeout = 90_000), Trait("Lane", "integration")]
    public async Task Sources_sync_answers_JobStarted_and_the_job_leaves_counts_and_the_synced_event()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var onyx = await FakeOnyxServer.Start();
        await using var host = await SyncHost(onyx);

        var (job, body) = await Start(host, "/sindri/context/sources/sync", "{}");
        await JobHost.Settle(host, Token);
        var events = await Ledger(host);

        Assert.True(body.GetProperty("accepted").GetProperty("sequence").GetInt64() > 0);
        Assert.Equal($"/sindri/jobs/{job}", body.GetProperty("links").GetProperty("job").GetString());
        var finished = Json(Assert.Single(Of(events, EventTypes.JobFinished)).Envelope.Payload);
        Assert.Equal("succeeded", finished.GetProperty("outcome").GetString());
        var row = finished.GetProperty("result").GetProperty("sources").EnumerateArray().Single(r => r.GetProperty("source").GetString() == "onyx");
        Assert.True(row.GetProperty("links").GetProperty("linked").GetInt32() > 0);
        Assert.DoesNotContain("Toggle split orientation", finished.GetRawText(), StringComparison.Ordinal);   // counts and codes only, never record text
        var synced = Of(events, EventTypes.ContextSourcesSynced).Select(e => Json(e.Envelope.Payload).GetProperty("source").GetString()).ToList();
        Assert.Equal(finished.GetProperty("result").GetProperty("sources").GetArrayLength(), synced.Count);   // one event per source the job synced
        Assert.Contains(synced, s => s == "onyx");
        var jobView = Json((await host.Http.Ask(Get($"/sindri/jobs/{job}", Human(host.Token.Value)))).Body);
        Assert.Equal(("succeeded", "SyncSources"), (jobView.GetProperty("state").GetString(), jobView.GetProperty("kind").GetString()));
    }

    [Fact(Timeout = 90_000), Trait("Lane", "integration")]
    public async Task Sources_sync_names_one_source_and_an_unknown_one_fails_the_job_with_058()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var onyx = await FakeOnyxServer.Start();
        await using var host = await SyncHost(onyx);

        _ = await Start(host, "/sindri/context/sources/sync", """{"source":"onyx"}""");
        await JobHost.Settle(host, Token);
        _ = await Start(host, "/sindri/context/sources/sync", """{"source":"nothing"}""");
        await JobHost.Settle(host, Token);

        var finished = Of(await Ledger(host), EventTypes.JobFinished).Select(e => Json(e.Envelope.Payload)).ToList();
        Assert.Equal(["failed", "succeeded"], finished.Select(f => f.GetProperty("outcome").GetString()).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Contains(finished, f => f.TryGetProperty("code", out var code) && code.GetString() == "SND-CONTEXT-058");
    }

    [Fact(Timeout = 90_000), Trait("Lane", "integration")]
    public async Task A_source_that_is_not_kebab_case_is_refused_at_the_start_as_058()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var onyx = await FakeOnyxServer.Start();
        await using var host = await SyncHost(onyx);

        var refused = await host.Http.Ask(Post("/sindri/context/sources/sync", """{"source":"Not A Source"}""", Human(host.Token.Value)));

        Assert.Equal(("SND-CONTEXT-058", HttpStatusCode.NotFound), (refused.Code, refused.Status));
        Assert.Empty(Of(await Ledger(host), EventTypes.JobStarted));
    }

    [Fact(Timeout = 90_000), Trait("Lane", "integration")]
    public async Task A_second_sync_start_while_one_runs_is_SND_JOB_409_naming_the_running_job_and_056_maps_onto_it()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var onyx = await FakeOnyxServer.Start(new FakeServerOptions { Delay = TimeSpan.FromMilliseconds(40) });
        await using var host = await SyncHost(onyx);
        var (first, _) = await Start(host, "/sindri/context/sources/sync", "{}");

        var second = await host.Http.Ask(Post("/sindri/context/sources/sync", "{}", Human(host.Token.Value)));
        await JobHost.Settle(host, Token);

        Assert.Equal((HttpStatusCode.Conflict, "SND-JOB-409"), (second.Status, second.Code));
        Assert.Equal(first, Json(second.Body).GetProperty("runningJob").GetString());
        Assert.Equal(("SND-JOB-409", 409), (Core.SindriErrors.JobConflict.Code, (int)HttpStatusCode.Conflict));
        Assert.Equal(Kernel.ErrorKind.Conflict, Context.Core.ContextErrors.SyncRunning.Kind);   // 056 stays the in-run code of a sync that meets a running one (the scheduled service's); both are 409
        _ = Assert.Single(Of(await Ledger(host), EventTypes.JobStarted));
    }

    [Fact(Timeout = 90_000), Trait("Lane", "integration")]
    public async Task A_one_source_sync_start_while_an_all_sources_sync_runs_is_SND_JOB_409()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var onyx = await FakeOnyxServer.Start(new FakeServerOptions { Delay = TimeSpan.FromMilliseconds(40) });
        await using var host = await SyncHost(onyx);
        var (first, _) = await Start(host, "/sindri/context/sources/sync", "{}");

        var second = await host.Http.Ask(Post("/sindri/context/sources/sync", """{"source":"onyx"}""", Human(host.Token.Value)));

        Assert.Equal((HttpStatusCode.Conflict, "SND-JOB-409"), (second.Status, second.Code));
        Assert.Equal(first, Json(second.Body).GetProperty("runningJob").GetString());   // one key for every sync: the docs promise one sync job at a time
    }

    [Fact(Timeout = 90_000), Trait("Lane", "integration")]
    public async Task Sources_sync_accepts_an_empty_body_like_map_build()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var onyx = await FakeOnyxServer.Start();
        await using var host = await SyncHost(onyx);

        var started = await host.Http.Ask(Post("/sindri/context/sources/sync", string.Empty, Human(host.Token.Value)));
        await JobHost.Settle(host, Token);

        Assert.True(started.Status == HttpStatusCode.OK, started.Body);
    }

    [Fact(Timeout = 90_000), Trait("Lane", "integration")]
    public async Task An_all_sources_sync_that_meets_056_on_one_source_still_records_the_others_and_reports_it_as_that_sources_row()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var onyx = await FakeOnyxServer.Start();
        await using var host = await SyncHost(onyx, services => SyncRunningOnZeta.Install(services));

        _ = await Start(host, "/sindri/context/sources/sync", "{}");
        await JobHost.Settle(host, Token);
        var events = await Ledger(host);

        var finished = Json(Assert.Single(Of(events, EventTypes.JobFinished)).Envelope.Payload);
        Assert.Equal("succeeded", finished.GetProperty("outcome").GetString());
        var rows = finished.GetProperty("result").GetProperty("sources").EnumerateArray().ToDictionary(r => r.GetProperty("source").GetString()!, r => r.GetProperty("links"), StringComparer.Ordinal);
        Assert.True(rows["onyx"].GetProperty("linked").GetInt32() > 0);
        Assert.Equal("SND-CONTEXT-056", rows["zeta"].GetProperty("code").GetString());
        var synced = Of(events, EventTypes.ContextSourcesSynced).Select(e => Json(e.Envelope.Payload).GetProperty("source").GetString()).ToList();
        Assert.Equal(["onyx"], synced);   // the source that finished has its event; the one that was running has the run that owns it
    }

    [Fact(Timeout = 90_000), Trait("Lane", "integration")]
    public async Task A_session_may_start_the_build_but_not_the_source_sync()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var onyx = await FakeOnyxServer.Start();
        await using var host = await SyncHost(onyx);
        (string Name, string Value)[] session = [(SindriHeaders.Caller, "session"), (SindriHeaders.Repo, FixtureMap.Repo)];

        var sync = await host.Http.Ask(Post("/sindri/context/sources/sync", "{}", session));
        var build = await host.Http.Ask(Post("/sindri/context/map/build", "{}", session));

        Assert.Equal(("SND-AUTH-002", null), (sync.Code, build.Code));   // build opened to sessions per owner q93 (2026-10-23); the sync stays a human's
        Assert.Contains("sindri context sources sync", sync.Body, StringComparison.Ordinal);
        _ = Assert.Single(Of(await Ledger(host), EventTypes.JobStarted));
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Map_build_answers_JobStarted_and_the_job_writes_a_draft_and_the_built_event()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await BuildHost();

        var (job, _) = await Start(host, "/sindri/context/map/build", "{}");
        await JobHost.Settle(host, Token);
        var events = await Ledger(host);

        var finished = Json(Assert.Single(Of(events, EventTypes.JobFinished)).Envelope.Payload);
        Assert.Equal("succeeded", finished.GetProperty("outcome").GetString());
        var result = finished.GetProperty("result");
        Assert.True(result.GetProperty("draft").GetInt32() > 0);
        Assert.True(result.GetProperty("proposed").GetInt32() > 0);
        var built = Json(Assert.Single(Of(events, EventTypes.ContextMapBuilt)).Envelope.Payload);
        Assert.Equal(result.GetProperty("draft").GetInt32(), built.GetProperty("draft").GetInt32());
        var progress = Of(events, EventTypes.JobProgress).Select(e => Json(e.Envelope.Payload).GetProperty("line").GetString()).ToList();
        Assert.NotEmpty(progress);   // the engine's step lines reach the job
        Assert.Equal(HttpStatusCode.OK, (await host.Http.Ask(Get("/sindri/context/map/review", Human(host.Token.Value)))).Status);   // a draft exists to review
        Assert.False(string.IsNullOrEmpty(job));
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Every_job_event_after_the_start_carries_the_start_as_its_cause()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await BuildHost();

        _ = await Start(host, "/sindri/context/map/build", "{}");
        await JobHost.Settle(host, Token);
        var events = (await Ledger(host)).Where(e => e.Envelope.Type.StartsWith("job.", StringComparison.Ordinal)).ToList();

        var started = events.Single(e => e.Envelope.Type == EventTypes.JobStarted);
        Assert.All(events.Where(e => e.Envelope.Type != EventTypes.JobStarted), e => Assert.Equal(started.Envelope.Id, e.Envelope.CausationId));
    }
}
