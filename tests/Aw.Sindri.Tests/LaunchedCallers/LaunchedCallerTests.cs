using System.Net;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Host;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Tests.Callers;
using Microsoft.Extensions.DependencyInjection;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.LaunchedCallers;

/// <summary>
/// A launched agent (#1002): a request that carries a launch's secret is that launch's session, whatever else it can read (the human
/// token file sits in its environment), and since agent-first PR 2 that session may answer a decision like the owner: no SND-AUTH-002.
/// </summary>
public sealed class LaunchedCallerTests(LaunchedCallerHost host) : IClassFixture<LaunchedCallerHost>
{
    private const string LaunchHeader = "X-Sindri-Launch";
    private const string Secret = "proof";
    private const string Launch = "LCH-00000000000000000000000000000a01";
    private const string Decision = "DEC-00000000000000000000000000000b01";

    [Fact, Trait("Lane", "integration")]
    public async Task Launch_secret_with_this_starts_human_token_is_a_session()
    {
        var answer = await host.Http.Ask(Get("/test/launched-caller", [.. Human(host.Token.Value), (SindriHeaders.LaunchSecret, "proof")]));

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal("Session", answer.Body);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_launched_session_gets_past_the_caller_check_on_a_decision_answer()
    {
        LaunchSecrets.Write(host.App.Services.GetRequiredService<LauncherOptions>().SecretsDirectory, LaunchId.From(Launch), Secret);
        (string Name, string Value)[] session = [(SindriHeaders.Caller, "session"), (SindriHeaders.Client, "cli"), (LaunchHeader, Launch)];

        var answer = await host.Http.Ask(Request(session));

        Assert.Equal((HttpStatusCode.ServiceUnavailable, "SND-API-011"), (answer.Status, answer.Code));   // past the caller check: this host runs no engine, so the answer stops there
    }

    private static HttpRequestMessage Request((string Name, string Value)[] headers) =>
        Post($"/sindri/decisions/{Decision}", """{"choice":"accept"}""", headers);
}
