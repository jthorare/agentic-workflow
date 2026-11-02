using Aw.Kernel.Json;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Infrastructure.Sandbox.Launch;
using Aw.Sindri.Tests.Ledger;
using Aw.Sindri.Tests.Sandbox.Plan;
using Req = Aw.Sindri.Testing.Sandbox.SandboxRequests;

namespace Aw.Sindri.Tests.Sandbox.Seatbelt;

/// <summary>A session is refused on a Mac too: with the Mac registration in place, 'sandbox login' and 'sandbox exec' from a launched session are SND-AUTH-002 before the verb is resolved.</summary>
[Collection(ProcessEnvironment.Name), Trait("Lane", "integration")]
public sealed class SeatbeltVerbsSessionRefusalTests : IDisposable
{
    private readonly TempHome _home = new();
    private readonly StringWriter _error = new();
    private readonly TextWriter _consoleOut = Console.Out;
    private readonly TextWriter _consoleError = Console.Error;

    public SeatbeltVerbsSessionRefusalTests()
    {
        Console.SetOut(new StringWriter());
        Console.SetError(_error);
    }

    public void Dispose()
    {
        Console.SetOut(_consoleOut);
        Console.SetError(_consoleError);
        _home.Dispose();
    }

    private string? Environment(string name) => name switch
    {
        "AW_SINDRI_HOME" => _home.Path,
        "AW_SINDRI_LAUNCH" => Req.WriteScope.Launch.Value,
        "HOME" => "/Users/owner",
        _ => null,
    };

    private async Task<int> RunOnMac(params string[] args)
    {
        _ = Directory.CreateDirectory(Path.Combine(_home.Path, "auth"));
        await File.WriteAllTextAsync(Path.Combine(_home.Path, "auth", "human.token"), "a-human-token\n", TestContext.Current.CancellationToken);   // a launched session never calls as a human, whatever it can read
        return await CommandTable.Run(args, Environment, TimeProvider.System, services => services.AddSindriPlatformSandbox(Environment, linux: false, uid: 501, mac: true));
    }

    [Fact]
    public async Task A_session_is_refused_for_sandbox_login()
    {
        var exit = await RunOnMac("sandbox", "login", "claude-code");

        Assert.Equal(2, exit);
        Assert.StartsWith("SND-AUTH-002 ", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_session_is_refused_for_sandbox_exec()
    {
        var directory = Path.Combine(_home.Path, "area");
        _ = Directory.CreateDirectory(directory);
        await File.WriteAllBytesAsync(Path.Combine(directory, "sandbox-plan.json"), AwJson.Serialize(SandboxPlannerTests.Build(Req.WriteScope), AwJson.CreateOptions(SindriJsonContext.Default)), TestContext.Current.CancellationToken);

        var exit = await RunOnMac("sandbox", "exec", directory);

        Assert.Equal(2, exit);
        Assert.StartsWith("SND-AUTH-002 ", _error.ToString(), StringComparison.Ordinal);
    }
}
