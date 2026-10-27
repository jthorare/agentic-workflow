using System.Net;
using Aw.Sindri.Core.Auth;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Host;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Tests.Ledger;
using Microsoft.Extensions.DependencyInjection;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.Callers;

/// <summary>The caller middleware on the real host (API spec §6): the human token, the caller header and the prover label.</summary>
public sealed class CallerAuthTests(CallerHost host) : IClassFixture<CallerHost>
{
    [Fact, Trait("Lane", "integration")]
    public async Task Human_without_a_token_is_403_SND_AUTH_001()
    {
        var answer = await host.Http.Ask(Get("/sindri/status", Human(token: null)));

        Assert.Equal((HttpStatusCode.Forbidden, "SND-AUTH-001"), (answer.Status, answer.Code));
        Assert.Contains("restart the app or rerun the command", answer.Body, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Human_with_a_stale_token_is_403_SND_AUTH_001()
    {
        var answer = await host.Http.Ask(Get("/sindri/status", Human("token-of-an-earlier-start")));

        Assert.Equal((HttpStatusCode.Forbidden, "SND-AUTH-001"), (answer.Status, answer.Code));
        Assert.DoesNotContain(host.Token.Value, answer.Body, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Human_query_with_the_token_answers()
    {
        var answer = await host.Http.Ask(Get("/sindri/status", Human(host.Token.Value)));

        Assert.Equal(HttpStatusCode.OK, answer.Status);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Session_query_needs_no_token()
    {
        Assert.Equal(HttpStatusCode.OK, (await host.Http.Ask(Get("/sindri/status"))).Status);
        Assert.Equal(HttpStatusCode.OK, (await host.Http.Ask(Get("/sindri/status", (SindriHeaders.Caller, "session"), (SindriHeaders.Client, "cli")))).Status);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Unknown_caller_is_400_SND_API_007()
    {
        var answer = await host.Http.Ask(Get("/sindri/status", (SindriHeaders.Caller, "admin")));

        Assert.Equal((HttpStatusCode.BadRequest, "SND-API-007"), (answer.Status, answer.Code));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Prover_label_is_honoured_for_a_temp_home()
    {
        var answer = await host.Http.Ask(Get("/sindri/status", Human(host.Token.Value, client: "prover")));

        Assert.Equal(HttpStatusCode.OK, answer.Status);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Prover_label_is_refused_when_the_home_is_not_temporary()
    {
        using var home = new TempHome();
        using var elsewhere = new TempHome();
        var token = new HumanToken("token-of-a-real-home");
        await using var app = await CallerHost.Start(
            n => n switch { "AW_SINDRI_HOME" => home.Path, "TMPDIR" => Path.Combine(elsewhere.Path, "tmp"), "AW_STATE_DIR" => Path.Combine(elsewhere.Path, "state"), _ => null },
            token);
        using var http = new HttpClient { BaseAddress = new Uri(app.Urls.First()), Timeout = TimeSpan.FromSeconds(10) };

        var prover = await http.Ask(Get("/sindri/status", Human(token.Value, client: "prover")));
        var cli = await http.Ask(Get("/sindri/status", Human(token.Value)));

        Assert.Equal((HttpStatusCode.Forbidden, "SND-AUTH-001"), (prover.Status, prover.Code));
        Assert.Contains("prover label", prover.Body, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, cli.Status);
        await app.StopAsync(TestContext.Current.CancellationToken);
    }

    private const string Launch = "LCH-00000000000000000000000000000001";

    [Fact, Trait("Lane", "integration")]
    public async Task A_session_with_an_unknown_launch_is_403_SND_AUTH_003()
    {
        var answer = await host.Http.Ask(Get("/sindri/status", (SindriHeaders.Launch, "LCH-000000000000000000000000000000ff")));

        Assert.Equal((HttpStatusCode.Forbidden, "SND-AUTH-003"), (answer.Status, answer.Code));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_session_with_an_open_launch_answers()
    {
        LaunchSecrets.Write(host.App.Services.GetRequiredService<LauncherOptions>().SecretsDirectory, LaunchId.From(Launch), "proof");

        var answer = await host.Http.Ask(Get("/sindri/status", (SindriHeaders.Launch, Launch)));

        Assert.Equal(HttpStatusCode.OK, answer.Status);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task An_open_launch_lands_on_the_callers_context_as_a_session_even_under_a_human_header()
    {
        LaunchSecrets.Write(host.App.Services.GetRequiredService<LauncherOptions>().SecretsDirectory, LaunchId.From(Launch), "proof");

        var answer = await host.Http.Ask(Get("/test/caller", (SindriHeaders.Caller, "human"), (SindriHeaders.Launch, Launch)));

        Assert.Equal((HttpStatusCode.OK, "Session|" + Launch), (answer.Status, answer.Body));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_launch_secret_makes_the_caller_a_session_even_with_this_starts_human_token()
    {
        var answer = await host.Http.Ask(Get("/test/caller", [.. Human(host.Token.Value), (SindriHeaders.LaunchSecret, "proof")]));

        Assert.Equal((HttpStatusCode.OK, "Session|"), (answer.Status, answer.Body));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_malformed_launch_header_is_400_SND_API_007()
    {
        var answer = await host.Http.Ask(Get("/sindri/status", (SindriHeaders.Launch, "nope")));

        Assert.Equal((HttpStatusCode.BadRequest, "SND-API-007"), (answer.Status, answer.Code));
    }
}
