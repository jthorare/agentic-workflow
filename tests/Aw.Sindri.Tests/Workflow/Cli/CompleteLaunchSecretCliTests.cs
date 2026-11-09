using Aw.Sindri.Host.Commands;
using Aw.Sindri.Tests.Ledger;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Workflow.Cli;

/// <summary>
/// 'sindri workflow complete' from a launched agent session (engine P8; API spec §8.1): the session's launch proof (AW_SINDRI_LAUNCH_SECRET)
/// travels as the X-Sindri-Launch-Secret header, through the verb table exactly as Program runs it. The control plane here is the real host
/// behind a recording stand-in for the route the verb calls.
/// </summary>
[Collection(ProcessEnvironment.Name)]
public sealed class CompleteLaunchSecretCliTests : IDisposable
{
    private const string Proof = "proof-value-one";
    private readonly TempHome _home = new();
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();
    private readonly TextWriter _consoleOut = Console.Out;
    private readonly TextWriter _consoleError = Console.Error;

    public CompleteLaunchSecretCliTests()
    {
        Console.SetOut(_out);
        Console.SetError(_error);
    }

    public void Dispose()
    {
        Console.SetOut(_consoleOut);
        Console.SetError(_consoleError);
        _home.Dispose();
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task A_launched_sessions_complete_carries_its_launch_proof_as_the_header()
    {
        var ct = TestContext.Current.CancellationToken;
        var seen = new List<string?>();
        await using var app = await Started(seen, ct);

        _ = await CommandTable.Run(
            ["workflow", "complete", "ITM-1/step/1", "--run", "1.1", "--outcome", "done", "--nonce", new string('a', 32)],
            name => name switch { "AW_SINDRI_HOME" => _home.Path, "AW_SINDRI_LAUNCH_SECRET" => Proof, _ => null },
            TimeProvider.System);

        Assert.Equal([Proof], seen);
    }

    /// <summary>A control plane in the temp home (CliHost) whose complete route is answered and recorded here.</summary>
    private Task<WebApplication> Started(List<string?> proofs, CancellationToken ct) =>
        CliHost.Started(_home, ct, app => app.Use((context, next) =>
        {
            if (!context.Request.Path.Value!.EndsWith("/complete", StringComparison.Ordinal))
            {
                return next(context);
            }

            proofs.Add(context.Request.Headers["X-Sindri-Launch-Secret"].FirstOrDefault());
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            return Task.CompletedTask;
        }));
}
