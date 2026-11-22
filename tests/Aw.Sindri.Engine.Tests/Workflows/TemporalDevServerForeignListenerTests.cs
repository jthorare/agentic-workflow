using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Tests.Ledger;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// #3767: between a start's port pick and the server's bind, another listener can take the frontend port. In CI it was a time-skipping
/// test server of a parallel test class: it answers DescribeNamespace but has no operator service, so the start got "ListSearchAttributes
/// is unimplemented" instead of seeing that its own server lost the port and picking again. Here the foreign listener is a fake gRPC
/// frontend and the "dev server" is a script that reports "address already in use" and exits, so the race happens every time.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class TemporalDevServerForeignListenerTests : IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    [Fact(Timeout = 60_000)]
    public async Task A_foreign_listener_on_the_picked_frontend_port_is_a_lost_port_and_the_start_picks_again()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var foreign = await FakeFrontend.Start(operatorServed: false);   // answers DescribeNamespace, says unimplemented to the operator service
        await using var real = await FakeFrontend.Start(operatorServed: true);       // stands for the server of the second attempt
        var picks = new Queue<int>([foreign.Port, FreePort(), FreePort(), real.Port, FreePort(), FreePort()]);
        var server = new TemporalDevServer(ChildEnvironment.Start, picks.Dequeue);
        var start = new TemporalServerStart(FakeServerScript(foreign.Port), _home.Path);

        var running = await server.StartAsync(start, ct);
        try
        {
            Assert.Equal(real.Port, running.Frontend.Port);
            Assert.Contains("picking new ports", await File.ReadAllTextAsync(Path.Combine(_home.Path, "logs", SindriTemporal.LogFile), ct), StringComparison.Ordinal);
        }
        finally
        {
            await server.StopAsync(running, ct);
        }
    }

    // the first run loses the port a second after it starts (the probe has answered by then); later runs stay up
    private string FakeServerScript(int lostPort)
    {
        var script = Path.Combine(_home.Path, "fake-temporal.sh");
        var ran = Path.Combine(_home.Path, "ran");
        File.WriteAllText(script, string.Create(CultureInfo.InvariantCulture, $"#!/bin/sh\nif [ -e '{ran}' ]; then exec sleep 60; fi\n: > '{ran}'\nsleep 1\necho 'listen tcp 127.0.0.1:{lostPort}: bind: address already in use'\nexit 1\n"));
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        return script;
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private sealed class FakeFrontend : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private FakeFrontend(WebApplication app, int port)
        {
            _app = app;
            Port = port;
        }

        public int Port { get; }

        public static async Task<FakeFrontend> Start(bool operatorServed)
        {
            var builder = WebApplication.CreateSlimBuilder();
            _ = builder.WebHost.ConfigureKestrel(static k => k.Listen(IPAddress.Loopback, 0, static o => o.Protocols = HttpProtocols.Http2));
            var app = builder.Build();
            app.Run(context => Answer(context, operatorServed));
            await app.StartAsync(TestContext.Current.CancellationToken);
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            return new FakeFrontend(app, new Uri(address).Port);
        }

        public async ValueTask DisposeAsync() => await _app.DisposeAsync();

        private static async Task Answer(HttpContext context, bool operatorServed)
        {
            context.Response.ContentType = "application/grpc";
            if (!operatorServed && (context.Request.Path.Value ?? string.Empty).Contains("OperatorService", StringComparison.Ordinal))
            {
                context.Response.Headers.GrpcStatus = "12";   // UNIMPLEMENTED, a trailers-only answer
                return;
            }

            await context.Response.Body.WriteAsync(new byte[5], context.RequestAborted);   // an empty message
            context.Response.AppendTrailer("grpc-status", "0");
        }
    }
}
