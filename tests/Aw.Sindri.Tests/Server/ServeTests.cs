using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using Aw.Kernel.Services;
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
        await using var app = LoopbackBinding.Build([], n => n switch { "AW_SINDRI_HOME" => _home.Path, _ => null }, TimeProvider.System);
        _ = Pipeline.Configure(app);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var started = await scope.ServiceProvider.GetRequiredService<IService<StartProcessRequest, StartProcessResponse>>().Handle(new StartProcessRequest(), Token);
            Assert.True(started.IsSuccess);
        }

        await app.StartAsync(Token);
        using var http = new HttpClient { BaseAddress = new Uri(app.Urls.First()), Timeout = TimeSpan.FromSeconds(10) };

        var status = await http.GetFromJsonAsync("/sindri/status", HttpJson.Info<GetStatusResponse>(), Token);
        var ping = await http.GetFromJsonAsync("/sindri/ping", HttpJson.Info<Ping>(), Token);
        var events = await FirstEventLines(http);
        var stop = await http.PostAsync(new Uri("/sindri/stop", UriKind.Relative), content: null, Token);

        Assert.True(status!.Running);
        Assert.Equal((Environment.ProcessId, status.Epoch), (ping!.Pid, ping.Epoch));
        Assert.Contains("event: process.started", events, StringComparison.Ordinal);
        Assert.Contains("id: 1", events, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, stop.StatusCode);
        Assert.Null(app.Services.GetRequiredService<HeldLease>().Current);
        Assert.True(app.Services.GetRequiredService<ProcessShutdown>().Stopping.IsCancellationRequested);
        await app.StopAsync(ct);
    }

    [Theory(Timeout = Deadline), Trait("Lane", "integration")]
    [InlineData("""{"hash":"nope","confirmed":true}""")]
    [InlineData("""{"hash":""")]
    public async Task A_bad_approve_body_is_a_400_problem_with_SND_CLI_003_and_no_stack_trace(string json)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = LoopbackBinding.Build([], n => n switch { "AW_SINDRI_HOME" => _home.Path, _ => null }, TimeProvider.System);
        _ = Pipeline.Configure(app);
        await app.StartAsync(Token);
        using var http = new HttpClient { BaseAddress = new Uri(app.Urls.First()), Timeout = TimeSpan.FromSeconds(10) };

        using var body = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var answer = await http.PostAsync(new Uri("/sindri/profile/approve", UriKind.Relative), body, Token);
        var text = await answer.Content.ReadAsStringAsync(Token);

        Assert.Equal(HttpStatusCode.BadRequest, answer.StatusCode);
        Assert.Equal("SND-CLI-003", System.Text.Json.JsonDocument.Parse(text).RootElement.GetProperty("title").GetString());
        Assert.DoesNotContain("   at ", text, StringComparison.Ordinal);
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

        var code = await ServeCommand.Run(n => n switch { "AW_SINDRI_HOME" => _home.Path, "AW_SINDRI_PORT" => port, _ => null }, TimeProvider.System, error)
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
            static services => services.AddHostedService<FailsToStart>()).WaitAsync(ct);

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

        var code = await ServeCommand.Run(n => n switch { "AW_SINDRI_HOME" => _home.Path, "AW_SINDRI_PORT" => port, _ => null }, TimeProvider.System, error)
            .WaitAsync(ct);

        Assert.Equal(2, code);
        Assert.StartsWith($"SND-CLI-003 AW_SINDRI_PORT='{port}' isn't a port number (fix: ", error.ToString(), StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(_home.Path, "ledger.db")));
    }

    [Fact(Timeout = Deadline), Trait("Lane", "integration")]
    public async Task Approving_a_stale_hash_through_the_api_exits_2_with_SND_PROFILE_003()
    {
        var ct = TestContext.Current.CancellationToken;
        _ = Directory.CreateDirectory(Path.Combine(_home.Path, "profile"));
        await File.WriteAllTextAsync(Path.Combine(_home.Path, "profile", "profile.yaml"), "schemaVersion: 1\nuser: thor\n", ct);
        await using var app = LoopbackBinding.Build([], n => n switch { "AW_SINDRI_HOME" => _home.Path, _ => null }, TimeProvider.System);
        _ = Pipeline.Configure(app);
        await using var scope = app.Services.CreateAsyncScope();
        var grant = (await scope.ServiceProvider.GetRequiredService<IService<StartProcessRequest, StartProcessResponse>>().Handle(new StartProcessRequest(), ct)).Value.Grant;
        await app.StartAsync(ct);
        _ = await app.Services.GetRequiredService<IEndpointFile>().Write(new ControlEndpoint(new Uri(app.Urls.First()), grant.Holder.Pid, grant.Epoch), ct);
        var stale = SliceHarness.Profile("9s").Hash;
        var stderr = new StringWriter();
        var probe = app.Services.GetRequiredService<IControlPlaneProbe>();
        using var client = new ControlPlaneClient(probe, SliceHarness.Json);
        var verbs = new Verbs(client, probe, new Output(new StringWriter(), stderr, SliceHarness.Json), scope.ServiceProvider);

        var code = await verbs.Approve(stale.Value, interactive: true, new StringReader(stale.Abbreviated + "\n"), ct);

        Assert.Equal(2, code);
        Assert.StartsWith("SND-PROFILE-003 ", stderr.ToString(), StringComparison.Ordinal);
        await app.StopAsync(ct);
    }
}
