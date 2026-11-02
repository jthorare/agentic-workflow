using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Host;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;
using Microsoft.Extensions.DependencyInjection;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>'session wrap''s two routes on a real host (API spec §8.1): what lands in the ledger, what is deleted, and who may call.</summary>
[Lane(TestLane.Integration)]
public sealed class SessionRoutesTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<IReadOnlyList<LedgerEvent>> Ledger(GoldenHost host) =>
        (await host.App.Services.GetRequiredService<ILedgerStore>().ReadAfter(0, 1000, Token)).Value;

    [Fact]
    public async Task A_heartbeat_with_the_session_id_appends_launch_session_reported_and_no_secret_reaches_the_ledger()
    {
        await using var host = await GoldenHost.Start(temporary: true, Token, ItemScenarios.ProfileYaml);
        var launch = await WrapSessionScenarios.StartLaunch(host, Token);

        var answer = await host.Http.Ask(Post(WrapSessionScenarios.Route(launch, "heartbeat"), $$"""{"sessionId":"{{WrapSessionScenarios.ReportedId}}"}""", WrapSessionScenarios.Wrap(WrapSessionScenarios.Proof)));

        Expect.Equal(HttpStatusCode.OK, answer.Status);
        var events = await Ledger(host);
        var reported = Assert.Single(events, static e => e.Envelope.Type == EventTypes.LaunchSessionReported);
        Expect.Equal($"launch.session-reported:{launch.Value}", reported.IdempotencyKey);
        Expect.True(events.All(static e => !e.Envelope.Payload.Contains(WrapSessionScenarios.Proof, StringComparison.Ordinal)));
        Expect.True(!events.Any(static e => e.Envelope.Type == EventTypes.SessionEnded));
    }

    [Fact]
    public async Task An_exit_appends_session_ended_with_the_code_and_deletes_the_hash_so_a_late_call_is_refused()
    {
        await using var host = await GoldenHost.Start(temporary: true, Token, ItemScenarios.ProfileYaml);
        var launch = await WrapSessionScenarios.StartLaunch(host, Token);
        var hash = LaunchSecrets.PathFor(new LauncherOptions(host.Home).SecretsDirectory, launch);

        var exited = await host.Http.Ask(Post(WrapSessionScenarios.Route(launch, "exit"), """{"exitCode":7}""", WrapSessionScenarios.Wrap(WrapSessionScenarios.Proof)));
        var late = await host.Http.Ask(Post(WrapSessionScenarios.Route(launch, "heartbeat"), "{}", WrapSessionScenarios.Wrap(WrapSessionScenarios.Proof)));

        Expect.Equal((HttpStatusCode.OK, HttpStatusCode.Forbidden, false), (exited.Status, late.Status, File.Exists(hash)));
        Expect.True(late.Body.Contains("SND-AUTH-003", StringComparison.Ordinal));
        var ended = Assert.Single(await Ledger(host), static e => e.Envelope.Type == EventTypes.SessionEnded);
        Expect.True(ended.Envelope.Payload.Contains("\"exitCode\":7", StringComparison.Ordinal) || ended.Envelope.Payload.Contains("\"exitCode\": 7", StringComparison.Ordinal));
    }

    [Fact]
    public async Task An_exit_with_a_memory_limit_appends_session_ended_reason_limit()
    {
        await using var host = await GoldenHost.Start(temporary: true, Token, ItemScenarios.ProfileYaml);
        var launch = await WrapSessionScenarios.StartLaunch(host, Token);

        var exited = await host.Http.Ask(Post(WrapSessionScenarios.Route(launch, "exit"),
            """{"exitCode":137,"limit":{"kind":"memory","limit":2000000000,"peak":2200000000,"peaks":{"memoryBytes":2200000000,"cpuPercent":80,"tasks":12,"wallSeconds":300}}}""",
            WrapSessionScenarios.Wrap(WrapSessionScenarios.Proof)));

        Expect.Equal(HttpStatusCode.OK, exited.Status);
        var payload = Assert.Single(await Ledger(host), static e => e.Envelope.Type == EventTypes.SessionEnded).Envelope.Payload.Replace(" ", string.Empty, StringComparison.Ordinal);
        Expect.True(payload.Contains("\"reason\":\"limit\"", StringComparison.Ordinal) && payload.Contains("\"kind\":\"memory\"", StringComparison.Ordinal)
            && payload.Contains("\"limit\":2000000000", StringComparison.Ordinal) && payload.Contains("\"peak\":2200000000", StringComparison.Ordinal) && payload.Contains("\"peaks\":{", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Wrong_secret_is_SND_AUTH_003()
    {
        await using var host = await GoldenHost.Start(temporary: true, Token, ItemScenarios.ProfileYaml);
        var launch = await WrapSessionScenarios.StartLaunch(host, Token);

        var wrong = await host.Http.Ask(Post(WrapSessionScenarios.Route(launch, "heartbeat"), "{}", WrapSessionScenarios.Wrap("not-the-proof")));
        var missing = await host.Http.Ask(Post(WrapSessionScenarios.Route(launch, "exit"), """{"exitCode":0}""", (SindriHeaders.Caller, "session")));

        Expect.Equal((HttpStatusCode.Forbidden, HttpStatusCode.Forbidden), (wrong.Status, missing.Status));
        Expect.True(wrong.Body.Contains("SND-AUTH-003", StringComparison.Ordinal) && missing.Body.Contains("SND-AUTH-003", StringComparison.Ordinal));
        Expect.True(!(await Ledger(host)).Any(static e => e.Envelope.Type == EventTypes.SessionEnded));
    }

    [Fact]
    public async Task Suspended_answers_false_for_a_running_launch_with_its_secret_and_no_bearer_token()
    {
        await using var host = await GoldenHost.Start(temporary: true, Token, ItemScenarios.ProfileYaml);
        var launch = await WrapSessionScenarios.StartLaunch(host, Token);

        var answer = await host.Http.Ask(Get($"/sindri/launches/{launch.Value}/suspended", WrapSessionScenarios.Wrap(WrapSessionScenarios.Proof)));

        Expect.Equal(HttpStatusCode.OK, answer.Status);
        Expect.True(answer.Body.Contains("\"suspended\":false", StringComparison.Ordinal) || answer.Body.Contains("\"suspended\": false", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Suspended_with_a_wrong_secret_is_SND_AUTH_003()
    {
        await using var host = await GoldenHost.Start(temporary: true, Token, ItemScenarios.ProfileYaml);
        var launch = await WrapSessionScenarios.StartLaunch(host, Token);

        var answer = await host.Http.Ask(Get($"/sindri/launches/{launch.Value}/suspended", WrapSessionScenarios.Wrap("not-the-proof")));

        Expect.Equal(HttpStatusCode.Forbidden, answer.Status);
        Expect.True(answer.Body.Contains("SND-AUTH-003", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_malformed_launch_id_is_a_400()
    {
        await using var host = await GoldenHost.Start(temporary: true, Token, ItemScenarios.ProfileYaml);

        var answer = await host.Http.Ask(Post("/sindri/sessions/not-a-launch/heartbeat", "{}", WrapSessionScenarios.Wrap(WrapSessionScenarios.Proof)));

        Expect.Equal(HttpStatusCode.BadRequest, answer.Status);
    }
}
