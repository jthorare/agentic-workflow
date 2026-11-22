using System.Net;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using Aw.Connectors.Testing.Server;
using Aw.Connectors.Testing.Slack;
using static Aw.Connectors.Testing.Slack.SlackWorkspaceSeed;

namespace Aw.Connectors.Tests.Slack;

/// <summary>The fake Slack's loopback host (messages spec §8.1): it binds 127.0.0.1 only and serves the same fake over HTTP, scripts included.</summary>
public sealed class FakeSlackHostTests
{
    private const string Token = "xo" + "xp-fixture-" + "fedcba9876543210";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static IEnumerable<IPAddress> OtherAddressesOfThisBox()
        => NetworkInterface.GetAllNetworkInterfaces()
            .Where(static n => n.OperationalStatus == OperationalStatus.Up)
            .SelectMany(static n => n.GetIPProperties().UnicastAddresses)
            .Select(static a => a.Address)
            .Where(static a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a));

    [Fact, Trait("Lane", "integration")]
    public async Task The_host_binds_loopback_only()
    {
        await using var host = await FakeSlackHost.Start(new FakeSlackServer().SeedWorkspace(), cancellationToken: Ct);

        var address = Assert.Single(host.Addresses);
        Assert.True(IPAddress.IsLoopback(IPAddress.Parse(address.Host)), address.ToString());
        Assert.Equal("127.0.0.1", host.BaseUrl.Host);
        foreach (var other in OtherAddressesOfThisBox())
        {
            using var socket = new Socket(other.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            using var wait = CancellationTokenSource.CreateLinkedTokenSource(Ct);
            wait.CancelAfter(TimeSpan.FromSeconds(5));
            var refused = await Record.ExceptionAsync(async () => await socket.ConnectAsync(new IPEndPoint(other, host.BaseUrl.Port), wait.Token));
            Assert.True(refused is SocketException or OperationCanceledException, other.ToString() + " accepted a connection on the fake's port");
        }
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_host_serves_the_fake_over_http_with_the_token_and_a_scripted_429()
    {
        await using var host = await FakeSlackHost.Start(new FakeSlackServer().SeedWorkspace(), cancellationToken: Ct);
        using var client = new HttpClient { BaseAddress = host.BaseUrl };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);

        using var auth = await client.GetAsync(new Uri("api/auth.test", UriKind.Relative), Ct);
        var body = JsonNode.Parse(await auth.Content.ReadAsStringAsync(Ct))!;
        _ = host.Server.ScriptRateLimit("conversations.history", 7);
        using var limited = await client.GetAsync(new Uri("api/conversations.history?channel=" + DirectMessage, UriKind.Relative), Ct);

        Assert.Equal(Me, body["user_id"]!.GetValue<string>());
        Assert.Equal(FakeSlackServer.ReleaseOneScopes, Assert.Single(auth.Headers.GetValues("x-oauth-scopes")));
        var seen = host.Server.Requests[0];
        Assert.Equal(new Uri("https://slack.com/api/auth.test"), seen.Uri);
        Assert.Equal("Bearer " + Token, seen.Authorization);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(7), limited.Headers.RetryAfter?.Delta);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_host_answers_500_for_a_method_the_fake_does_not_serve()
    {
        await using var host = await FakeSlackHost.Start(new FakeSlackServer(), cancellationToken: Ct);
        using var client = new HttpClient { BaseAddress = host.BaseUrl };

        using var response = await client.GetAsync(new Uri("api/chat.postMessage", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("unexpected route", await response.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }
}
