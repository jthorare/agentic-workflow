using Aw.Kernel.Services;
using Aw.Sindri.Core.Auth;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Features.StartProcess;
using Aw.Sindri.Host;
using Aw.Sindri.Tests.Ledger;
using Aw.Sindri.Tests.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Workflow.Cli;

/// <summary>A control plane in a temp home for the CLI-over-HTTP tests (Temporal faked): the lease held, this start's token minted and held, its endpoint recorded.</summary>
internal static class CliHost
{
    /// <param name="home">The temp home the host lives in.</param>
    /// <param name="ct">Cancels the start.</param>
    /// <param name="beforePipeline">Adds middleware ahead of the pipeline (a test that makes the host slow to answer).</param>
    public static async Task<WebApplication> Started(TempHome home, CancellationToken ct, Action<WebApplication>? beforePipeline = null)
    {
        _ = Directory.CreateDirectory(Path.Combine(home.Path, "profile"));
        await File.WriteAllTextAsync(Path.Combine(home.Path, "profile", "profile.yaml"), "schemaVersion: 1\nuser: thor\n", ct);
        var app = LoopbackBinding.Build([], n => n switch { "AW_SINDRI_HOME" => home.Path, _ => null }, TimeProvider.System, NoTemporal.Use);   // the CLI verbs here don't use Temporal: a real dev server only slows the start under load
        beforePipeline?.Invoke(app);
        _ = Pipeline.Configure(app);
        Core.Lease.LeaseGrant grant;
        await using (var scope = app.Services.CreateAsyncScope())
        {
            grant = (await scope.ServiceProvider.GetRequiredService<IService<StartProcessRequest, StartProcessResponse>>().Handle(new StartProcessRequest(), ct)).Value.Grant;
        }

        app.Services.GetRequiredService<HeldHumanToken>().Hold((await app.Services.GetRequiredService<IHumanTokenStore>().Mint(ct)).Value);
        await app.StartAsync(ct);
        _ = await app.Services.GetRequiredService<IEndpointFile>().Write(new ControlEndpoint(new Uri(app.Urls.First()), grant.Holder.Pid, grant.Epoch), ct);
        await WarmUp(app, ct);
        return app;
    }

    /// <summary>
    /// Answers the first request here, with a generous budget: Kestrel builds an endpoint lazily on its first request, which takes longer than the CLI's
    /// 2 s ping budget on a loaded box. The CLI's budget is product behaviour; the host is answering before a test calls it (#1053).
    /// </summary>
    private static async Task WarmUp(WebApplication app, CancellationToken ct)
    {
        using var http = new HttpClient(new SocketsHttpHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(30) };
        using var response = await http.GetAsync(new Uri(new Uri(app.Urls.First()), "/sindri/ping"), ct);
        _ = response.EnsureSuccessStatusCode();
    }
}
