using Aw.Kernel.Json;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Tests.Ledger;
using Aw.Sindri.Tests.Sandbox.Plan;
using Req = Aw.Sindri.Testing.Sandbox.SandboxRequests;

namespace Aw.Sindri.Tests.Sandbox;

/// <summary>'sindri sandbox explain' (sandbox spec §3.6): a pure lookup in the plan file. It runs with the state directory denied, offline, and builds no service.</summary>
[Collection(ProcessEnvironment.Name)]
public sealed class SandboxExplainCliTests : IDisposable
{
    private readonly TempHome _work = new();
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();
    private readonly TextWriter _consoleOut = Console.Out;
    private readonly TextWriter _consoleError = Console.Error;
    private readonly string _denied;

    public SandboxExplainCliTests()
    {
        Console.SetOut(_out);
        Console.SetError(_error);
        _denied = Path.Combine(_work.Path, "state");
        _ = Directory.CreateDirectory(_denied);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(_denied, UnixFileMode.None);   // mode 000: any attempt to open the state directory fails
        }
    }

    public void Dispose()
    {
        Console.SetOut(_consoleOut);
        Console.SetError(_consoleError);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(_denied, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        _work.Dispose();
    }

    private string PlanFile()
    {
        var file = Path.Combine(_work.Path, "plan.json");
        File.WriteAllBytes(file, AwJson.Serialize(SandboxPlannerTests.Build(Req.WriteScope), AwJson.CreateOptions(SindriJsonContext.Default)));
        return file;
    }

    private Func<string, string?> Environment(string? plan) => name => name switch
    {
        "AW_SINDRI_HOME" => Path.Combine(_denied, "home"),
        "AW_STATE_DIR" => Path.Combine(_denied, "state"),
        "AW_SANDBOX_PLAN" => plan,
        _ => null,
    };

    [Fact, Trait("Lane", "integration")]
    public async Task Explain_reads_only_the_plan_file()
    {
        var exit = await CommandTable.Run(["sandbox", "explain", "evil.example"], Environment(PlanFile()), TimeProvider.System);

        var answer = SandboxPlanRenderer.Explain(SandboxPlannerTests.Build(Req.WriteScope), "evil.example");
        Assert.Equal(0, exit);
        Assert.Equal(answer.EndsWith('\n') ? answer : answer + "\n", _out.ToString());   // one trailing newline, never two
        Assert.Empty(_error.ToString());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_plan_flag_wins_over_the_variable_and_a_command_may_be_several_words()
    {
        var exit = await CommandTable.Run(["sandbox", "explain", "dotnet", "build", "--plan", PlanFile()], Environment(Path.Combine(_work.Path, "missing.json")), TimeProvider.System);

        Assert.Equal(0, exit);
        Assert.Contains("dotnet build", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Json_carries_the_query_and_the_answer()
    {
        var exit = await CommandTable.Run(["sandbox", "explain", "evil.example", "--json"], Environment(PlanFile()), TimeProvider.System);

        Assert.Equal(0, exit);
        using var document = System.Text.Json.JsonDocument.Parse(_out.ToString());
        Assert.Equal("evil.example", document.RootElement.GetProperty("query").GetString());
        Assert.Contains("evil.example", document.RootElement.GetProperty("answer").GetString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task No_plan_file_is_a_one_line_error_with_exit_2()
    {
        var exit = await CommandTable.Run(["sandbox", "explain", "evil.example"], Environment(plan: null), TimeProvider.System);

        Assert.Equal(2, exit);
        Assert.Empty(_out.ToString());
        Assert.StartsWith("SND-CLI-003 ", _error.ToString(), StringComparison.Ordinal);
        _ = Assert.Single(_error.ToString().TrimEnd().Split('\n'));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task An_unreadable_plan_file_is_a_one_line_error_with_exit_2()
    {
        var file = Path.Combine(_work.Path, "broken.json");
        await File.WriteAllTextAsync(file, "{ not a plan", TestContext.Current.CancellationToken);

        var exit = await CommandTable.Run(["sandbox", "explain", "evil.example", "--plan", file], Environment(plan: null), TimeProvider.System);

        Assert.Equal(2, exit);
        Assert.StartsWith("SND-CLI-003 ", _error.ToString(), StringComparison.Ordinal);
        _ = Assert.Single(_error.ToString().TrimEnd().Split('\n'));
    }
}
