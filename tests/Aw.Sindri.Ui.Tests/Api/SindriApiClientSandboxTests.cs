using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Sandbox;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Api;

/// <summary>The sandbox routes over the canned handler (wire contract items 4 and 8): what the app sends, as which caller.</summary>
[Lane(TestLane.Unit)]
public sealed class SindriApiClientSandboxTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static (SindriApiClient Client, FakeSindriApi Server, FakeSindriHandler Handler) Reach()
    {
        var server = new FakeSindriApi();
        var handler = new FakeSindriHandler(server);
        return (new SindriApiClient(new HttpClient(handler), new InMemoryHumanToken(A.HumanToken), TimeProvider.System), server, handler);
    }

    [Fact]
    public async Task SandboxStatus_reads_the_status_route_as_the_human_without_a_query()
    {
        var (client, _, handler) = Reach();

        var status = await client.SandboxStatus(_api, Token);

        var seen = Assert.Single(handler.Requests);
        Assert.Equal(("GET", "/sindri/sandbox/status", string.Empty), (seen.Method, seen.Path, seen.Query));
        Assert.Equal(("human", "ui", A.HumanToken), (seen.Caller, seen.Client, seen.Token));
        Assert.True(status.Value.Ready);
    }

    [Fact]
    public async Task SandboxCheck_reads_the_check_route()
    {
        var (client, _, handler) = Reach();

        var check = await client.SandboxCheck(_api, Token);

        var seen = Assert.Single(handler.Requests);
        Assert.Equal(("GET", "/sindri/sandbox/check", string.Empty), (seen.Method, seen.Path, seen.Query));
        Assert.False(check.Value.Ready);
    }

    [Fact]
    public async Task SandboxDenials_sends_the_item_as_the_query()
    {
        var (client, _, handler) = Reach();

        _ = await client.SandboxDenials(_api, "ITM-0000000000000000000000000000000f", Token);

        var seen = Assert.Single(handler.Requests);
        Assert.Equal(("GET", "/sindri/sandbox/denials", "?item=ITM-0000000000000000000000000000000f"), (seen.Method, seen.Path, seen.Query));
    }

    [Fact]
    public async Task SandboxAllow_posts_the_request_as_the_human()
    {
        var (client, server, handler) = Reach();
        var request = new AllowRequest("LCH-00000000000000000000000000000001", "path", "/home/thor/.config/tool");

        var allowed = await client.SandboxAllow(_api, request, Token);

        var seen = Assert.Single(handler.Requests);
        Assert.Equal(("POST", "/sindri/sandbox/allow", string.Empty), (seen.Method, seen.Path, seen.Query));
        Assert.Equal(("human", "ui"), (seen.Caller, seen.Client));
        Assert.Equal("needs-approval", allowed.Value.State);
        Assert.Equal(request, Assert.Single(server.Allowed));
    }
}
