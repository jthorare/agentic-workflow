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
        var launch = await SessionScenarios.StartLaunch(host, Token);

        var answer = await host.Http.Ask(Post(SessionScenarios.Route(launch, "heartbeat"), $$"""{"sessionId":"{{SessionScenarios.ReportedId}}"}""", SessionScenarios.Wrap(SessionScenarios.Proof)));

        Expect.Equal(HttpStatusCode.OK, answer.Status);
        var events = await Ledger(host);
        var reported = Assert.Single(events, static e => e.Envelope.Type == EventTypes.LaunchSessionReported);
        Expect.Equal($"launch.session-reported:{launch.Value}", reported.IdempotencyKey);
        Expect.True(events.All(static e => !e.Envelope.Payload.Contains(SessionScenarios.Proof, StringComparison.Ordinal)));
        Expect.True(!events.Any(static e => e.Envelope.Type == EventTypes.SessionEnded));
    }

    [Fact]
    public async Task An_exit_appends_session_ended_with_the_code_and_deletes_the_hash_so_a_late_call_is_refused()
    {
        await using var host = await GoldenHost.Start(temporary: true, Token, ItemScenarios.ProfileYaml);
        var launch = await SessionScenarios.StartLaunch(host, Token);
        var hash = LaunchSecrets.PathFor(new LauncherOptions(host.Home).SecretsDirectory, launch);

        var exited = await host.Http.Ask(Post(SessionScenarios.Route(launch, "exit"), """{"exitCode":7}""", SessionScenarios.Wrap(SessionScenarios.Proof)));
        var late = await host.Http.Ask(Post(SessionScenarios.Route(launch, "heartbeat"), "{}", SessionScenarios.Wrap(SessionScenarios.Proof)));

        Expect.Equal((HttpStatusCode.OK, HttpStatusCode.Forbidden, false), (exited.Status, late.Status, File.Exists(hash)));
        Expect.True(late.Body.Contains("SND-AUTH-003", StringComparison.Ordinal));
        var ended = Assert.Single(await Ledger(host), static e => e.Envelope.Type == EventTypes.SessionEnded);
        Expect.True(ended.Envelope.Payload.Contains("\"exitCode\":7", StringComparison.Ordinal) || ended.Envelope.Payload.Contains("\"exitCode\": 7", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Wrong_secret_is_SND_AUTH_003()
    {
        await using var host = await GoldenHost.Start(temporary: true, Token, ItemScenarios.ProfileYaml);
        var launch = await SessionScenarios.StartLaunch(host, Token);

        var wrong = await host.Http.Ask(Post(SessionScenarios.Route(launch, "heartbeat"), "{}", SessionScenarios.Wrap("not-the-proof")));
        var missing = await host.Http.Ask(Post(SessionScenarios.Route(launch, "exit"), """{"exitCode":0}""", (SindriHeaders.Caller, "session")));

        Expect.Equal((HttpStatusCode.Forbidden, HttpStatusCode.Forbidden), (wrong.Status, missing.Status));
        Expect.True(wrong.Body.Contains("SND-AUTH-003", StringComparison.Ordinal) && missing.Body.Contains("SND-AUTH-003", StringComparison.Ordinal));
        Expect.True(!(await Ledger(host)).Any(static e => e.Envelope.Type == EventTypes.SessionEnded));
    }

    [Fact]
    public async Task A_malformed_launch_id_is_a_400()
    {
        await using var host = await GoldenHost.Start(temporary: true, Token, ItemScenarios.ProfileYaml);

        var answer = await host.Http.Ask(Post("/sindri/sessions/not-a-launch/heartbeat", "{}", SessionScenarios.Wrap(SessionScenarios.Proof)));

        Expect.Equal(HttpStatusCode.BadRequest, answer.Status);
    }
}
