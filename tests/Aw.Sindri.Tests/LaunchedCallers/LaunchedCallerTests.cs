using System.Net;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Host;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Tests.Callers;
using Microsoft.Extensions.DependencyInjection;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.LaunchedCallers;

/// <summary>
/// A launched agent (#1002): a request that carries a launch's secret is that launch's session, whatever else it can read (the human
/// token file sits in its environment), and a session is refused the human-only verbs with SND-AUTH-002.
/// </summary>
public sealed class LaunchedCallerTests(LaunchedCallerHost host) : IClassFixture<LaunchedCallerHost>
{
    private const string LaunchHeader = "X-Sindri-Launch";
    private const string Secret = "proof";
    private const string Launch = "LCH-00000000000000000000000000000a01";
    private const string Instance = "ITM-00000000000000000000000000000c01/line/1/scoping.1";
    private const string Decision = "DEC-00000000000000000000000000000b01";
    private const string RecipeHash = "sha256:0000000000000000000000000000000000000000000000000000000000000000";

    [Fact, Trait("Lane", "integration")]
    public async Task Launch_secret_with_this_starts_human_token_is_a_session()
    {
        var answer = await host.Http.Ask(Get("/test/launched-caller", [.. Human(host.Token.Value), (SindriHeaders.LaunchSecret, "proof")]));

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal("Session", answer.Body);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("takeover")]
    [InlineData("cancel")]
    [InlineData("start")]
    [InlineData("decide")]
    [InlineData("approve")]
    public async Task A_launched_session_is_refused_a_human_only_verb_with_SND_AUTH_002(string verb)
    {
        LaunchSecrets.Write(host.App.Services.GetRequiredService<LauncherOptions>().SecretsDirectory, LaunchId.From(Launch), Secret);
        (string Name, string Value)[] session = [(SindriHeaders.Caller, "session"), (SindriHeaders.Client, "cli"), (LaunchHeader, Launch)];

        var answer = await host.Http.Ask(Request(verb, session));

        Assert.Equal((HttpStatusCode.Forbidden, "SND-AUTH-002"), (answer.Status, answer.Code));
    }

    private static HttpRequestMessage Request(string verb, (string Name, string Value)[] headers) => verb switch
    {
        "takeover" => Post($"/sindri/workflow/instances/{Uri.EscapeDataString(Instance)}/commands", """{"kind":"takeover"}""", headers),
        "cancel" => Post($"/sindri/workflow/instances/{Uri.EscapeDataString(Instance)}/commands", """{"kind":"cancel"}""", headers),
        "start" => Post("/sindri/workflow/instances", """{"recipe":"workflow.step","item":"ITM-00000000000000000000000000000c01","nextAttempt":false,"facts":{}}""", headers),
        "decide" => Post($"/sindri/decisions/{Decision}", """{"choice":"accept"}""", headers),
        _ => Post($"/sindri/workflow/recipes/workflow.step@{RecipeHash}/approve", json: null, headers),
    };
}
