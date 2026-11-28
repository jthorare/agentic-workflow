using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Ledger;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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

    /// <summary>One command line per Dispatch arm (and sub-arm) that CliCompositions sends to the client container.</summary>
    public static TheoryData<string> ClientArms => [
        "", "--headless", "status", "status --follow", "stop", "views rebuild --no-start", "handoff", "open", "ledger",
        "queue", "items", "item add title", "project list", "projects", "connections", "memories", "resources", "toolkits",
        "requests", "decisions", "decide r1 yes", "needs-you", "services", "jobs", "index embed", "index status",
        "agents", "skills", "code-index status", "library sync", "setup --apply", "repo add /nowhere", "repo onboard",
        "launch probe", "launch picks",
        "context sources sync", "context map build", "context map refresh", "context show x", "context eval backtest",
        "context filemap accept l1", "context page list", "context search words", "context subgraph x", "context code x",
        "context lenses", "context map whole", "context map", "context query words",
        "wf create w --template t", "wf edit w --base 1 --ops []", "wf activate w", "wf pause w", "wf delete w",
        "wf validate --slug w", "wf run w", "wf cancel r1", "wf rerun r1", "wf seen r1", "wf takeover r1 n1",
        "wf handback r1 n1", "wf list", "wf blocks", "wf docs", "wf show w", "wf runs w", "wf run-show r1", "answer r1 yes"];

    /// <summary>
    /// Every Client arm resolves only what ComposeClient registers: a GetRequiredService of a full-only type under a client
    /// prefix throws "No service for type" here. Serve is down and a start fails at once, so the exit code doesn't matter.
    /// </summary>
    [Theory, Trait("Lane", "integration")]
    [MemberData(nameof(ClientArms))]
    public async Task A_client_verb_resolves_nothing_the_client_container_lacks(string line)
    {
        var args = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(CliComposition.Client, CliCompositions.For(args));

        _ = await CommandTable.Run(args, Environment, TimeProvider.System, services =>
        {
            _built = services;
            _ = services.Replace(ServiceDescriptor.Singleton<IControlPlaneLauncher>(FakeControlPlaneLauncher.ExitingWith("SND-CLI-002 no serve in this test")));
        });

        Assert.DoesNotContain("No service for type", _out + _error.ToString(), StringComparison.Ordinal);
        Assert.NotNull(_built);
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
