using System.Net;
using System.Text.Json;
using Aw.Kernel.Services;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Features.OnyxUp;
using Aw.Sindri.Host;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.ApiGoldens;
using Aw.Sindri.Tests.Callers;
using Aw.Sindri.Tests.Jobs;
using Aw.Sindri.Tests.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.SetupOnyx;

/// <summary>
/// The Onyx setup routes on the real host, a real SQLite ledger and the real runner (setup-once spec §5.6): Onyx's `up` is a stub and Onyx itself a fake, everything else is the served code.
/// The sentinel test sends one known login text down every path and searches every place it could have been written: the ledger, the jobs views, every answer and the logs.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class SetupOnyxRouteTests
{
    private const string Sentinel = "sentinel-route-value-4be07a";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Account(string email = "owner@example.com", string secret = Sentinel) => $"{{\"adminEmail\": \"{email}\", \"adminPassword\": \"{secret}\"}}";

    private static JsonElement Json(Answer answer) => JsonDocument.Parse(answer.Body).RootElement.Clone();

    private static async Task<IReadOnlyList<LedgerEvent>> Ledger(GoldenHost host) =>
        (await host.App.Services.GetRequiredService<ILedgerReader>().ReadAfter(0, 5000, Token)).Value;

    private static Task<Answer> Send(GoldenHost host, string path, string? json, params (string Name, string Value)[] headers) =>
        host.Http.Ask(Post(path, json, [.. JobHost.Human(host), .. headers]));

    private static async Task<JobId> StartSetup(GoldenHost host)
    {
        var started = await JobHost.Retrying(host, () => Send(host, "/sindri/onyx/setup", json: null), Token);
        Assert.True(started.Status == HttpStatusCode.OK, $"setup answered {started.Status} {started.Code}: {started.Body}");
        return JobId.From(Json(started).GetProperty("jobId").GetString()!);
    }

    private static async Task WaitUntilWaiting(GoldenHost host, JobId job)
    {
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        while (true)
        {
            var answer = await JobHost.Retrying(host, () => JobHost.Read(host, $"/sindri/jobs/{job.Value}"), Token);
            if (answer.Status == HttpStatusCode.OK && string.Equals(Json(answer).GetProperty("state").GetString(), "waiting", StringComparison.Ordinal))
            {
                return;
            }

            // The views catch up with the ledger a moment after the job is recorded, so a 404 is "not yet".
            Assert.True(System.Diagnostics.Stopwatch.GetElapsedTime(started) < JobHost.SettleHang, $"the job never showed as waiting: {answer.Status} {answer.Body}");
            await Task.Delay(20, Token);
        }
    }

    private sealed class Fakes
    {
        public FakeOnyxProvisioning Provisioning { get; } = new();

        public FakeSecretWriter Secrets { get; } = new();

        public FakeOnyxProfileWriter Profile { get; } = new();

        public FakeOnyxWorkspace Workspace { get; } = new();

        public FakeBrowser Browser { get; } = new();

        public ListLogger<object> Log { get; } = new();

        public void Install(IServiceCollection services) => services
            .Replace(ServiceDescriptor.Singleton<IOnyxProvisioning>(Provisioning))
            .Replace(ServiceDescriptor.Singleton<ISecretWriter>(Secrets))
            .Replace(ServiceDescriptor.Singleton<IOnyxProfileWriter>(Profile))
            .Replace(ServiceDescriptor.Singleton<IOnyxWorkspace>(Workspace))
            .Replace(ServiceDescriptor.Singleton<IBrowser>(Browser))
            .Replace(ServiceDescriptor.Singleton<IRandomHex>(new FakeRandomHex()))
            .Replace(ServiceDescriptor.Scoped<IService<OnyxUpRequest, OnyxUpResponse>>(_ => new SetupOnyxRig.StubOnyxUp()))
            .AddLogging(logging => logging.AddProvider(new CapturingProvider(Log)));
    }

    private sealed class CapturingProvider(ListLogger<object> log) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => log;

        public void Dispose()
        {
        }
    }

    [Fact(Timeout = 90_000)]
    public async Task The_setup_route_starts_the_job_which_shows_as_waiting_and_the_step_needs_you_with_ConnectOnyx()
    {
        var fakes = new Fakes();
        await using var host = await JobHost.Start(TestContext.Current.CancellationToken, more: fakes.Install);

        var job = await StartSetup(host);
        await WaitUntilWaiting(host, job);
        var waiting = Json(await JobHost.Retrying(host, () => JobHost.Read(host, "/sindri/jobs?state=waiting"), Token));
        var step = Json(await JobHost.Retrying(host, () => JobHost.Read(host, "/sindri/setup"), Token)).GetProperty("steps").EnumerateArray().Single(static s => string.Equals(s.GetProperty("id").GetString(), "onyx", StringComparison.Ordinal));
        var row = Json(await JobHost.Retrying(host, () => JobHost.Read(host, "/sindri/needs-you"), Token)).GetProperty("rows").EnumerateArray().Single(static r => string.Equals(r.GetProperty("id").GetString(), "failure:service:onyx", StringComparison.Ordinal));

        Assert.Equal([job.Value], waiting.GetProperty("jobs").EnumerateArray().Select(static j => j.GetProperty("id").GetString()), StringComparer.Ordinal);
        Assert.Equal(("needs-you", "ConnectOnyx"), (step.GetProperty("state").GetString(), step.GetProperty("action").GetString()));
        Assert.Contains("http://127.0.0.1:3000/auth/signup", step.GetProperty("detail").GetString(), StringComparison.Ordinal);
        Assert.Equal("ConnectOnyx", row.GetProperty("primaryAction").GetString());
        await JobHost.Cancel(host, job, Token);
        await JobHost.Settle(host, Token);
    }

    [Fact(Timeout = 90_000)]
    public async Task The_account_route_wakes_the_same_job_and_the_sentinel_is_nowhere_the_host_wrote_or_answered()
    {
        var fakes = new Fakes();
        await using var host = await JobHost.Start(TestContext.Current.CancellationToken, more: fakes.Install);
        var answers = new List<string>();
        var job = await StartSetup(host);
        await WaitUntilWaiting(host, job);

        var refusedShape = await Send(host, "/sindri/onyx/account", "{\"adminEmail\": \"owner@example.com\", \"" + Sentinel + "\"");   // malformed: the sentinel is in the body
        var missing = await Send(host, "/sindri/onyx/account", $"{{\"adminEmail\": \"owner@example.com\", \"note\": \"{Sentinel}\"}}");
        var connected = await Send(host, "/sindri/onyx/account", Account());
        await JobHost.Settle(host, Token);
        var view = await JobHost.Read(host, $"/sindri/jobs/{job.Value}");
        var list = await JobHost.Read(host, "/sindri/jobs");
        var late = await Send(host, "/sindri/onyx/account", Account());
        answers.AddRange([refusedShape.Body, missing.Body, connected.Body, view.Body, list.Body, late.Body]);

        Assert.Equal((HttpStatusCode.BadRequest, HttpStatusCode.BadRequest, HttpStatusCode.OK, HttpStatusCode.Conflict), (refusedShape.Status, missing.Status, connected.Status, late.Status));
        Assert.Equal(("SND-SETUP-005", "SND-SETUP-006"), (missing.Code, late.Code));
        Assert.Equal(job.Value, Json(connected).GetProperty("jobId").GetString());
        Assert.Equal("succeeded", Json(view).GetProperty("state").GetString());
        Assert.Equal("keychain:onyx/sindri-onyx", Json(view).GetProperty("result").GetProperty("keyPointer").GetString());
        Assert.Equal(("http://127.0.0.1:3000", "keychain:onyx/sindri-onyx", "v4.9.0"), fakes.Profile.Section!.Value);
        var written = string.Join('\n', (await Ledger(host)).SelectMany(static e => new[] { e.Envelope.Type, e.IdempotencyKey, e.Envelope.Payload }));
        Assert.Contains("job.waiting", written, StringComparison.Ordinal);
        Assert.Contains("job.resumed", written, StringComparison.Ordinal);
        foreach (var place in new[] { written, string.Join('\n', answers), string.Join('\n', fakes.Log.Lines) })
        {
            Assert.DoesNotContain(Sentinel, place, StringComparison.Ordinal);
        }
    }

    [Fact(Timeout = 90_000)]
    public async Task A_login_Onyx_refuses_fails_the_job_with_its_code_and_the_sentinel_stays_out_of_everything()
    {
        var fakes = new Fakes();
        fakes.Provisioning.SignInError = SindriErrors.OnyxKeyFailed.With("Onyx refused that login (HTTP 400)");
        await using var host = await JobHost.Start(TestContext.Current.CancellationToken, more: fakes.Install);
        var job = await StartSetup(host);
        await WaitUntilWaiting(host, job);

        var connected = await Send(host, "/sindri/onyx/account", Account());
        await JobHost.Settle(host, Token);
        var view = await JobHost.Read(host, $"/sindri/jobs/{job.Value}");

        Assert.Equal(("failed", SindriErrors.OnyxKeyFailed.Code), (Json(view).GetProperty("state").GetString(), Json(view).GetProperty("code").GetString()));
        var written = string.Join('\n', (await Ledger(host)).SelectMany(static e => new[] { e.Envelope.Type, e.IdempotencyKey, e.Envelope.Payload }));
        foreach (var place in new[] { written, connected.Body, view.Body, string.Join('\n', fakes.Log.Lines) })
        {
            Assert.DoesNotContain(Sentinel, place, StringComparison.Ordinal);
        }
    }

    [Fact(Timeout = 60_000)]
    public async Task The_account_route_with_no_job_waiting_is_a_409_and_a_session_is_refused_while_one_waits()
    {
        var fakes = new Fakes();
        await using var host = await JobHost.Start(TestContext.Current.CancellationToken, more: fakes.Install);

        var nobody = await Send(host, "/sindri/onyx/account", Account());
        var job = await StartSetup(host);
        await WaitUntilWaiting(host, job);
        var session = await host.Http.Ask(Post("/sindri/onyx/account", Account(), (SindriHeaders.Caller, "session"), (SindriHeaders.Client, "cli")));
        var stillWaiting = Json(await JobHost.Retrying(host, () => JobHost.Read(host, $"/sindri/jobs/{job.Value}"), Token)).GetProperty("state").GetString();

        Assert.Equal((HttpStatusCode.Conflict, "SND-SETUP-006"), (nobody.Status, nobody.Code));
        Assert.Equal((HttpStatusCode.Forbidden, "waiting"), (session.Status, stillWaiting));
        Assert.DoesNotContain(Sentinel, nobody.Body + session.Body + string.Join('\n', fakes.Log.Lines), StringComparison.Ordinal);
        await JobHost.Cancel(host, job, Token);
        await JobHost.Settle(host, Token);
    }

    [Fact(Timeout = 60_000)]
    public async Task The_defer_route_records_the_choice_and_a_missing_choice_is_a_400()
    {
        var fakes = new Fakes();
        await using var host = await JobHost.Start(TestContext.Current.CancellationToken, more: fakes.Install);

        var deferred = await Send(host, "/sindri/onyx/defer", "{\"deferred\": true}");
        var undone = await Send(host, "/sindri/onyx/defer", "{\"deferred\": false}");
        var missing = await Send(host, "/sindri/onyx/defer", "{}");

        Assert.Equal((HttpStatusCode.OK, true, HttpStatusCode.OK, false, HttpStatusCode.BadRequest), (deferred.Status, Json(deferred).GetProperty("deferred").GetBoolean(), undone.Status, Json(undone).GetProperty("deferred").GetBoolean(), missing.Status));
        Assert.Equal([true, false], (await Ledger(host)).Where(static e => string.Equals(e.Envelope.Type, "onyx.deferred", StringComparison.Ordinal))
            .Select(e => host.App.Services.GetRequiredService<Core.Events.EventFactory>().Payload<Core.Events.OnyxDeferred>(e).Value.Deferred));
    }
}
