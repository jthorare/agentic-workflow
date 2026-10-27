using System.Net;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Host;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Tests.Callers;
using Aw.Sindri.Tests.ContextCalls;
using Microsoft.Extensions.DependencyInjection;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.Mcp;

/// <summary>The MCP route keeps X-Sindri-Launch (q100): the caller middleware then checks it names an open launch, as it does for the HTTP API.</summary>
public sealed class McpLaunchHeaderTests(PlainMcpHost plain) : IClassFixture<PlainMcpHost>
{
    private const string Launch = "LCH-00000000000000000000000000000002";
    private const string NeverOpened = "LCH-000000000000000000000000000000fe";   // its own id: the other test opens Launch on the shared host

    [Fact, Trait("Lane", "integration")]
    public async Task An_unknown_launch_over_mcp_is_403_SND_AUTH_003()
    {
        var answer = await plain.Host.Http.Ask(Post("/sindri/mcp", "{}", (SindriHeaders.Repo, "alpha"), (SindriHeaders.Launch, NeverOpened)));

        Assert.Equal((HttpStatusCode.Forbidden, "SND-AUTH-003"), (answer.Status, answer.Code));
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task An_open_launch_over_mcp_is_kept_past_the_header_strip_and_connects()
    {
        var ct = TestContext.Current.CancellationToken;
        LaunchSecrets.Write(plain.Host.App.Services.GetRequiredService<LauncherOptions>().SecretsDirectory, LaunchId.From(Launch), "proof");

        await using var mcp = await McpSession.Open(plain.Host, "alpha", ct, (SindriHeaders.Launch, Launch));

        Assert.NotEmpty(await mcp.Client.ListToolsAsync(cancellationToken: ct));
    }
}
