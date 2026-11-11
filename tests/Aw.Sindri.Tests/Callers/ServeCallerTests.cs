using System.Net;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Auth;
using Aw.Sindri.Infrastructure.Control;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Ledger;
using Aw.Sindri.Tests.Server;
using Aw.Sindri.Tests.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.Callers;

/// <summary>'sindri serve' as the CLI runs it: the token's lifetime, and the in-process caller 'service:host' (API spec §2.1, §6).</summary>
public sealed class ServeCallerTests : IDisposable
{
    private static readonly TimeSpan _prompt = TimeSpan.FromSeconds(10);
    private readonly TempHome _home = new();
    private readonly StringWriter _said = new(System.Globalization.CultureInfo.InvariantCulture);   // why: #2881 answered 500 on CI with nothing to say why; serve's own lines go in the failure message
    private readonly ListLoggerFactory _logs = new();   // why: a 500's cause is only in serve's log; fields, not locals, so they outlive serve's task (CA2025)

    public void Dispose()
    {
        _said.Dispose();
        _logs.Dispose();
        _home.Dispose();
    }

    private SindriSettings Settings => new(_home.Path, Path.Combine(_home.Path, "profile", "profile.yaml"));

    private Func<string, string?> Environment => n => n switch { "AW_SINDRI_HOME" => _home.Path, "AW_SINDRI_PORT" => "0", _ => null };

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task Serve_mints_the_token_only_after_taking_the_lease()
    {
        var ct = TestContext.Current.CancellationToken;
        var serve = ServeCommand.Run(Environment, TimeProvider.System, _said, services =>
        {
            NoTemporal.Use(services);
            _ = services.Replace(ServiceDescriptor.Singleton<ILoggerFactory>(_logs));
        });
        var url = await Endpoint(ct);
        var token = (await new FileHumanToken(Settings).Read(ct)).Value;
        Assert.NotNull(token);

        var second = await ServeCommand.Run(Environment, TimeProvider.System, TextWriter.Null, NoTemporal.Use).WaitAsync(_prompt, ct);   // refused: the lease is held
        var afterRefusal = (await new FileHumanToken(Settings).Read(ct)).Value;
        using var http = new HttpClient { BaseAddress = url, Timeout = _prompt };
        var status = await http.Ask(Get("/sindri/status", Human(token.Value)));
        var stop = await http.Ask(Post("/sindri/stop", json: null, Human(token.Value)));
        var code = await serve.WaitAsync(_prompt, ct);

        Assert.Equal(1, second);
        Assert.Equal(token, afterRefusal);
        Assert.True(status.Status == HttpStatusCode.OK && stop.Status == HttpStatusCode.OK,
            $"status {status.Status}: {status.Body}\nstop {stop.Status}: {stop.Body}\nserve said: {_said}\nserve logged:\n{string.Join('\n', _logs.Logger.Lines)}\n{string.Join('\n', _logs.Logger.Exceptions)}");
        Assert.Equal(0, code);
        Assert.Null((await new FileHumanToken(Settings).Read(ct)).Value);   // removed with control.json
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task Stop_takes_the_real_dev_server_down_with_serve()
    {
        var ct = TestContext.Current.CancellationToken;
        var temporal = new DevServerTemporal(new TemporalSettings(_home.Path, PinnedTemporal.Path()), new TemporalDevServer(ChildEnvironment.Start));
        var serve = ServeCommand.Run(Environment, TimeProvider.System, TextWriter.Null, services =>
        {
            NoTemporal.Use(services);
            _ = services.Replace(ServiceDescriptor.Singleton<ITemporalServer>(temporal));
        });
        var stopped = false;
        try
        {
            var url = await Endpoint(ct, limitMs: 60_000);   // serve records it only after the dev server answers (up to 30 s)
            var token = (await new FileHumanToken(Settings).Read(ct)).Value;
            Assert.NotNull(token);
            using var http = new HttpClient { BaseAddress = url, Timeout = TimeSpan.FromSeconds(30) };

            var stop = await http.Ask(Post("/sindri/stop", json: null, Human(token.Value)));
            stopped = true;
            var code = await serve.WaitAsync(TimeSpan.FromSeconds(30), ct);

            // why: serve's stop takes its dev server down before it releases the lease; a server left here would outlive the test
            Assert.Equal((HttpStatusCode.OK, 0), (stop.Status, code));
            Assert.Empty(new TemporalDevServer(ChildEnvironment.Start).Find(_home.Path));
        }
        finally
        {
            if (!stopped)
            {
                _ = await temporal.Stop(CancellationToken.None);   // a red run leaves no dev server of its own behind
            }
        }
    }

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task Ctrl_C_on_serve_records_process_stopped_as_service_host_and_releases_the_lease()
    {
        var ct = TestContext.Current.CancellationToken;

        var code = await ServeCommand.Run(Environment, TimeProvider.System, TextWriter.Null,
            NoTemporal.And(static services => services.AddHostedService<StopsAsIfInterrupted>())).WaitAsync(_prompt, ct);

        var stores = SqliteStores.Create(_home.Path, TimeProvider.System, new FakeProcessProbe());
        var started = (await stores.Ledger.Latest(EventTypes.ProcessStarted, ct)).Value;
        var stopped = (await stores.Ledger.Latest(EventTypes.ProcessStopped, ct)).Value;
        Assert.Equal(0, code);
        Assert.Equal(("service:host", "service:host"), (started?.Envelope.Actor, stopped?.Envelope.Actor));
        Assert.Contains("\"reason\":\"signal\"", stopped!.Envelope.Payload, StringComparison.Ordinal);   // why: asserted recorded just above
        Assert.True((await stores.Lease.Read(ct)).Value!.Released);   // why: serve took the lease, so a row exists
        Assert.False(File.Exists(Settings.TokenPath));
    }

    /// <summary>The endpoint serve recorded once it listens.</summary>
    private async Task<Uri> Endpoint(CancellationToken ct, int limitMs = 10_000)
    {
        var file = new FileEndpoint(Settings, SliceHarness.Json);
        for (var waited = 0; ; waited += 50)
        {
            if ((await file.Read(ct)).Value is { } recorded)
            {
                return recorded.Url;
            }

            Assert.True(waited < limitMs, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"serve didn't record its endpoint within {limitMs / 1000} s"));
            await Task.Delay(50, ct);
        }
    }
}
