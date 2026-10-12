using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Registration;
using Aw.Sindri.Host.Server;
using Aw.Sindri.Tests.Profile;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Server;

/// <summary>Serve's port, chosen from the app's own container before it binds, on a real loopback host.</summary>
public sealed class ServePortTests : IDisposable
{
    private readonly ProfileFiles _files = new();

    public void Dispose() => _files.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "integration")]
    public async Task Without_the_variable_serve_listens_on_the_approved_api_port()
    {
        var free = FreePort();
        await Approve($"api: {{ port: {free} }}\n");

        Assert.Equal(new Uri($"http://127.0.0.1:{free}"), await Listening(fromEnvironment: null));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task AW_SINDRI_PORT_wins_over_the_approved_port()
    {
        var free = FreePort();
        await Approve("api: { port: 1 }\n".Replace("1", FreePort(), StringComparison.Ordinal));

        Assert.Equal(new Uri($"http://127.0.0.1:{free}"), await Listening(fromEnvironment: free));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Without_an_approval_or_the_variable_serve_listens_on_loopback_7410()
    {
        var listened = new List<IPEndPoint?>();
        await using var app = Build(services => services.Configure<KestrelServerOptions>(k => k.ConfigureEndpointDefaults(o => listened.Add(o.IPEndPoint))));
        var port = app.Services.GetRequiredService<ServePort>();

        var chosen = await port.Listen(fromEnvironment: null, Token);

        Assert.Equal((ApiSettings.DefaultPort, ApiSettings.DefaultPort), (chosen, port.Chosen));
        Assert.Equal([new IPEndPoint(IPAddress.Loopback, 7410)], listened);   // the one listener, added without starting the app
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_serve_host_started_before_Listen_refuses_to_start()
    {
        await using var app = Build();

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => app.StartAsync(Token));

        Assert.Contains("ServePort.Listen", refused.Message, StringComparison.Ordinal);
        Assert.Empty(app.Urls);
    }

    private async Task Approve(string api)
    {
        _files.Write("schemaVersion: 1\nuser: thor\n" + api);
        _ = await _files.Approvals.Save((await _files.Source.Load(Token)).Value, Token);
    }

    private async Task<Uri> Listening(string? fromEnvironment)
    {
        await using var app = Build();
        _ = await app.Services.GetRequiredService<ServePort>().Listen(fromEnvironment, Token);
        await app.StartAsync(Token);
        var url = new Uri(app.Urls.First());
        await app.StopAsync(Token);
        return url;
    }

    /// <summary>Serve's host in this test's home (no AW_SINDRI_PORT), plus <paramref name="configure"/>.</summary>
    private WebApplication Build(Action<IServiceCollection>? configure = null) =>
        LoopbackBinding.Build([], n => n switch { "AW_SINDRI_HOME" => _files.Settings.Home, _ => null }, TimeProvider.System, services =>
        {
            _ = services.AddSindriServe();
            configure?.Invoke(services);
        });

    private static string FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port.ToString(CultureInfo.InvariantCulture);
    }
}
