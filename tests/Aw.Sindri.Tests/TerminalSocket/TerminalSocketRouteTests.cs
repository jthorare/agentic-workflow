using System.Net;
using System.Net.WebSockets;
using System.Text;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.ApiGoldens;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.TerminalSocket;

/// <summary>GET /sindri/sessions/{provider}/{id}/terminal on a real loopback host (agents spec §15.8): the answers before the upgrade and the close after a bad token. The pane is a fake.</summary>
public sealed class TerminalSocketRouteTests
{
    private const string Route = "/sindri/sessions/claude-code/live/terminal";

    private static Task<GoldenHost> Start(CancellationToken ct) => GoldenHost.Start(temporary: true, ct, extra: static services =>
    {
        _ = services.Replace(ServiceDescriptor.Singleton<ITerminalPaneLocator>(new LivePane()));
        _ = services.Replace(ServiceDescriptor.Singleton<ITerminalPaneSource>(FakeTerminalPaneSource.FromFixture()));
    });

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task A_plain_GET_without_an_upgrade_is_a_400()
    {
        await using var host = await Start(TestContext.Current.CancellationToken);

        var answer = await host.Http.Ask(Get(Route, Human(host.Token.Value)));

        Assert.Equal((HttpStatusCode.BadRequest, SindriErrors.InvalidInput.Code), (answer.Status, answer.Code));
    }

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task A_foreign_Origin_is_a_403_before_the_upgrade()
    {
        await using var host = await Start(TestContext.Current.CancellationToken);
        using var request = Get(Route, [.. Human(host.Token.Value), ("Origin", "http://evil.example")]);

        var answer = await host.Http.Ask(request);

        Assert.Equal((HttpStatusCode.Forbidden, SindriErrors.OriginRefused.Code), (answer.Status, answer.Code));
    }

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task A_bad_token_in_the_first_message_closes_1008()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Start(ct);
        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(new Uri(new UriBuilder(host.Http.BaseAddress!) { Scheme = "ws", Path = Route }.ToString()), ct);

        await socket.SendAsync(Encoding.UTF8.GetBytes("""{"t":"hello","v":1,"token":"wrong","cols":120,"rows":36}"""), WebSocketMessageType.Text, endOfMessage: true, ct);
        var buffer = new byte[4096];
        WebSocketReceiveResult received;
        do
        {
            received = await socket.ReceiveAsync(buffer, ct);
        }
        while (received.MessageType != WebSocketMessageType.Close);

        Assert.Equal((WebSocketCloseStatus.PolicyViolation, SindriErrors.AuthRefused.Code), (received.CloseStatus, received.CloseStatusDescription));
    }

    private sealed class LivePane : ITerminalPaneLocator
    {
        public ValueTask<Result<TerminalTarget>> Find(SessionRef session, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<TerminalTarget>>(new TerminalTarget("sindri-launch", SocketIsPath: false, "live", "%0", Foreign: false));
    }
}
