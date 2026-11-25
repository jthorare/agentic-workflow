using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Tests.Ledger;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Commands;

/// <summary>
/// #3727 through the verb table as Program runs it: the usage builds no container, a verb that only calls the API builds
/// the client one, and an in-process verb the full one. It swaps the process's console, so it never runs beside another test.
/// </summary>
[Collection(ProcessEnvironment.Name)]
public sealed class CommandTableContainerTests : IDisposable
{
    private readonly TempHome _home = new();
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();
    private readonly TextWriter _consoleOut = Console.Out;
    private readonly TextWriter _consoleError = Console.Error;
    private IServiceCollection? _built;

    public CommandTableContainerTests()
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

    [Theory, Trait("Lane", "integration")]
    [InlineData("--help")]
    [InlineData("help")]
    [InlineData("-h")]
    public async Task The_usage_prints_without_building_a_container(string flag)
    {
        var code = await Run(flag);

        Assert.Equal((0, Usage.Text + "\n", string.Empty), (code, _out.ToString(), _error.ToString()));
        Assert.Null(_built);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Wf_list_with_serve_down_runs_in_the_client_container_without_the_ledger()
    {
        var code = await Run("wf", "list", "--json");

        Assert.Equal(1, code);   // no endpoint file in the home: not running
        Assert.NotNull(_built);
        Assert.Contains(_built, static d => d.ServiceType == typeof(IControlPlaneProbe));
        Assert.DoesNotContain(_built, static d => d.ServiceType == typeof(ILedgerStore));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Profile_validate_still_runs_in_the_full_container()
    {
        _ = await Run("profile", "validate");

        Assert.NotNull(_built);
        Assert.Contains(_built, static d => d.ServiceType == typeof(ILedgerStore));
    }

    private string? Environment(string name) => name switch { "AW_SINDRI_HOME" => _home.Path, _ => null };

    private Task<int> Run(params string[] args) =>
        CommandTable.Run(args, Environment, TimeProvider.System, services => _built = services);
}
