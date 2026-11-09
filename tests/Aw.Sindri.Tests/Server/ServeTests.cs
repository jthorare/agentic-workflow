using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Auth;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Features.StartProcess;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Host.Server;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Ledger;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Server;

/// <summary>
/// The control plane on a real loopback port with real adapters in a temp home, through the pipeline Program runs. Each
/// test has a deadline, so a hung socket or stream fails the test instead of hanging the lane.
/// </summary>
public sealed class ServeTests : IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    private const int Deadline = 30_000;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact(Timeout = Deadline), Trait("Lane", "integration")]
    public async Task The_api_serves_status_ping_events_and_stop_under_the_lease()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = LoopbackBinding.Build([], n => n switch { "AW_SINDRI_HOME" => _home.Path, _ => null }, TimeProvider.System, NoTemporal.Use);
        _ = Pipeline.Configure(app);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var started = await scope.ServiceProvider.GetRequiredService<IService<StartProcessRequest, StartProcessResponse>>().Handle(new StartProcessRequest(), Token);
            Assert.True(started.IsSuccess);
        }

        app.Services.GetRequiredService<HeldHumanToken>().Hold(new HumanToken("token-of-this-start"));   // serve mints it; the stop needs it
        await app.StartAsync(Token);
        using var http = new HttpClient { BaseAddress = new Uri(app.Urls.First()), Timeout = TimeSpan.FromSeconds(10) };

        var status = await http.GetFromJsonAsync("/sindri/status", HttpJson.Info<GetStatusResponse>(), Token);
        var ping = await http.GetFromJsonAsync("/sindri/ping", HttpJson.Info<Ping>(), Token);
        var events = await FirstEventLines(http);
        using var stopRequest = new HttpRequestMessage(HttpMethod.Post, "/sindri/stop").AsHuman(app);
        var stop = await http.SendAsync(stopRequest, Token);

        Assert.True(status!.Running);
        Assert.Equal((Environment.ProcessId, status.Epoch), (ping!.Pid, ping.Epoch));
        Assert.Contains("event: process.started", events, StringComparison.Ordinal);
        Assert.Contains("id: 1", events, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, stop.StatusCode);
        Assert.Null(app.Services.GetRequiredService<HeldLease>().Current);
        Assert.True(app.Services.GetRequiredService<ProcessShutdown>().Stopping.IsCancellationRequested);
        await app.StopAsync(ct);
    }

    private static async Task<string> FirstEventLines(HttpClient http)
    {
        using var cut = CancellationTokenSource.CreateLinkedTokenSource(Token);
        cut.CancelAfter(TimeSpan.FromSeconds(5));
        using var response = await http.GetAsync(new Uri("/sindri/events", UriKind.Relative), HttpCompletionOption.ResponseHeadersRead, cut.Token);
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(cut.Token));
        var lines = new List<string>();
        while (lines.Count < 3 && await reader.ReadLineAsync(cut.Token) is { } line)
        {
            lines.Add(line);
        }

        return string.Join('\n', lines);
    }

    [Fact(Timeout = Deadline), Trait("Lane", "integration")]
    public async Task Serve_on_a_taken_port_exits_2_with_SND_API_003()
    {
        var ct = TestContext.Current.CancellationToken;
        using var taken = new TcpListener(IPAddress.Loopback, 0);
        taken.Start();
        var port = ((IPEndPoint)taken.LocalEndpoint).Port.ToString(CultureInfo.InvariantCulture);
        var error = new StringWriter();

        var code = await ServeCommand.Run(n => n switch { "AW_SINDRI_HOME" => _home.Path, "AW_SINDRI_PORT" => port, _ => null }, TimeProvider.System, error, NoTemporal.Use)
            .WaitAsync(ct);

        Assert.Equal(2, code);
        Assert.StartsWith("SND-API-003 ", error.ToString(), StringComparison.Ordinal);
    }

    [Fact(Timeout = Deadline), Trait("Lane", "integration")]
    public async Task A_host_that_fails_to_start_after_the_lease_records_the_stop_releases_the_lease_and_exits_2()
    {
        var ct = TestContext.Current.CancellationToken;
        var error = new StringWriter();

        var code = await ServeCommand.Run(n => n switch { "AW_SINDRI_HOME" => _home.Path, "AW_SINDRI_PORT" => "0", _ => null }, TimeProvider.System, error,
            NoTemporal.And(static services => services.AddHostedService<FailsToStart>())).WaitAsync(ct);

        Assert.Equal(2, code);
        Assert.StartsWith("SND-API-003 serve couldn't start listening: ", error.ToString(), StringComparison.Ordinal);
        var stores = SqliteStores.Create(_home.Path, TimeProvider.System, new FakeProcessProbe());
        Assert.True((await stores.Lease.Read(ct)).Value!.Released);   // why: serve took the lease before it started the host
        var stopped = (await stores.Ledger.Latest(EventTypes.ProcessStopped, ct)).Value;
        Assert.Contains("\"reason\":\"listen-failed\"", stopped!.Envelope.Payload, StringComparison.Ordinal);   // why: the stop is recorded
    }

    [Theory(Timeout = Deadline), Trait("Lane", "integration")]
    [InlineData("seventy")]
    [InlineData("70000")]
    [InlineData("-1")]
    public async Task Serve_with_a_port_that_is_not_a_port_exits_2_with_SND_CLI_003_before_taking_the_lease(string port)
    {
        var ct = TestContext.Current.CancellationToken;
        var error = new StringWriter();

        var code = await ServeCommand.Run(n => n switch { "AW_SINDRI_HOME" => _home.Path, "AW_SINDRI_PORT" => port, _ => null }, TimeProvider.System, error, NoTemporal.Use)
            .WaitAsync(ct);

        Assert.Equal(2, code);
        Assert.StartsWith($"SND-CLI-003 AW_SINDRI_PORT='{port}' isn't a port number (fix: ", error.ToString(), StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(_home.Path, "ledger.db")));
    }

}
