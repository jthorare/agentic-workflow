using Aw.Kernel;
using Aw.Sindri.Core.Auth;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Ledger;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Workflow.Cli;

/// <summary>
/// A CLI verb against a host whose first request is slow (a loaded box: Kestrel builds an endpoint lazily on its first request). The CLI's
/// 2 s ping budget is product behaviour, so the test host must be answering before a test calls it (#1053).
/// </summary>
public sealed class ColdHostCliTests : IDisposable
{
    private readonly TempHome _home = new();
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    public void Dispose()
    {
        _out.Dispose();
        _error.Dispose();
        _home.Dispose();
    }

    [Fact(Timeout = 30_000), Trait("Lane", "integration")]
    public async Task Register_succeeds_when_the_hosts_first_request_is_slow()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await CliHost.Started(_home, ct, SlowFirstRequest);
        var file = Path.Combine(_home.Path, "workflow.step.yaml");
        await File.WriteAllTextAsync(file, ShippedRecipes.Read("sindri/workflow.step.yaml"), ct);

        var code = await Register(app, file, ct);

        Assert.True(code == 0, _error.ToString());
        _ = Assert.Single((await app.Services.GetRequiredService<IRecipeStore>().All(ct)).Value);
    }

    /// <summary>Holds the first request for longer than the CLI's 2 s ping budget, as a starved box does.</summary>
    private static void SlowFirstRequest(WebApplication app)
    {
        var first = 0;
        _ = app.Use(async (context, next) =>
        {
            if (Interlocked.Exchange(ref first, 1) == 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(3), context.RequestAborted);
            }

            await next(context);
        });
    }

    private async Task<int> Register(WebApplication app, string file, CancellationToken ct)
    {
        var session = new AgentSession(Terminal: true, Prover: false);
        using var client = new ControlPlaneClient(app.Services.GetRequiredService<IControlPlaneProbe>(), SliceHarness.Json,
            CallerHeaders.For(session, app.Services.GetRequiredService<IHumanTokenStore>(), static _ => ValueTask.FromResult<Core.Ids.RepoName?>(null)));
        var desk = new RecipeDesk(client, new StringReader(string.Empty), static _ => Task.FromResult<Error?>(null));
        await using var scope = app.Services.CreateAsyncScope();
        return await RecipeCommandTable.Run(["register", file], scope.ServiceProvider, new Output(_out, _error, SliceHarness.Json), desk, ct);
    }
}
