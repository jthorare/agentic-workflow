using Aw.Kernel.Services;
using Aw.Sindri.Core.Auth;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Features.StartProcess;
using Aw.Sindri.Host;
using Aw.Sindri.Tests.Ledger;
using Aw.Sindri.Tests.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.LaunchedCallers;

/// <summary>One control plane for the class (CallerHost.Start's steps) with a test-only route that answers the caller's class.</summary>
public sealed class LaunchedCallerHost : IAsyncLifetime
{
    private readonly TempHome _home = new();
    private WebApplication? _app;

    public HumanToken Token { get; } = new("token-of-this-start");

    public WebApplication App => _app ?? throw new InvalidOperationException("the host isn't started");

    public HttpClient Http { get; private set; } = new();

    public async ValueTask InitializeAsync()
    {
        var home = _home.Path;
        var app = LoopbackBinding.Build([], n => n switch { "AW_SINDRI_HOME" => home, _ => null }, TimeProvider.System, NoTemporal.Use);
        _ = Pipeline.Configure(app);
        _ = app.MapGet("/test/launched-caller", static (ICallerContextAccessor callers) =>
            Results.Text(callers.Current?.Class.ToString() ?? "none"));
        await using (var scope = app.Services.CreateAsyncScope())
        {
            _ = (await scope.ServiceProvider.GetRequiredService<IService<StartProcessRequest, StartProcessResponse>>()
                .Handle(new StartProcessRequest(), CancellationToken.None)).Value;
        }

        app.Services.GetRequiredService<HeldHumanToken>().Hold(Token);
        await app.StartAsync(CancellationToken.None);
        _app = app;
        Http = new HttpClient { BaseAddress = new Uri(app.Urls.First()), Timeout = TimeSpan.FromSeconds(10) };
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
}
