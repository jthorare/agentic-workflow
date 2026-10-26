using Aw.Kernel.Services;
using Aw.Sindri.Core.Auth;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Features.StartProcess;
using Aw.Sindri.Host;
using Aw.Sindri.Tests.Ledger;
using Aw.Sindri.Tests.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Callers;

/// <summary>
/// One control plane per test class for the caller tests: real adapters in a temp home (so the prover label is honoured),
/// the lease held, and a human token held, on a loopback port through the pipeline Program runs.
/// </summary>
public sealed class CallerHost : IAsyncLifetime
{
    private readonly TempHome _home = new();
    private WebApplication? _app;

    /// <summary>The token the host holds.</summary>
    public HumanToken Token { get; } = new("token-of-this-start");

    public string Home => _home.Path;

    public WebApplication App => _app ?? throw new InvalidOperationException("the host isn't started");

    public HttpClient Http { get; private set; } = new();

    public async ValueTask InitializeAsync()
    {
        _app = await Start(n => n switch { "AW_SINDRI_HOME" => _home.Path, _ => null }, Token, MapCallerEcho);
        Http = new HttpClient { BaseAddress = new Uri(_app.Urls.First()), Timeout = TimeSpan.FromSeconds(10) };
    }

    public async ValueTask DisposeAsync()
    {
        Http.Dispose();
        if (_app is not null)
        {
            await _app.StopAsync(CancellationToken.None);
            await _app.DisposeAsync();
        }

        _home.Dispose();
    }

    /// <summary>A test-only route that answers the class and launch the middleware put on the call: '&lt;class&gt;|&lt;launch&gt;'.</summary>
    private static void MapCallerEcho(WebApplication app) =>
        _ = app.MapGet("/test/caller", (ICallerContextAccessor callers) => callers.Current is { } caller ? caller.Class + "|" + caller.Launch?.Value : "none");

    /// <summary>Builds and starts a host over <paramref name="environment"/>, holding the lease and <paramref name="token"/>.</summary>
    public static async Task<WebApplication> Start(Func<string, string?> environment, HumanToken token, Action<WebApplication>? map = null)
    {
        var app = LoopbackBinding.Build([], environment, TimeProvider.System, NoTemporal.Use);   // the Temporal side is faked: these tests are about the API
        _ = Pipeline.Configure(app);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            _ = (await scope.ServiceProvider.GetRequiredService<IService<StartProcessRequest, StartProcessResponse>>()
                .Handle(new StartProcessRequest(), CancellationToken.None)).Value;
        }

        map?.Invoke(app);
        app.Services.GetRequiredService<HeldHumanToken>().Hold(token);
        await app.StartAsync(CancellationToken.None);
        return app;
    }
}
