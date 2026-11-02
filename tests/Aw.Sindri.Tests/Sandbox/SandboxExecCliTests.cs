using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Tests.Ledger;
using Aw.Sindri.Tests.Sandbox.Plan;
using Microsoft.Extensions.DependencyInjection;
using Req = Aw.Sindri.Testing.Sandbox.SandboxRequests;

namespace Aw.Sindri.Tests.Sandbox;

/// <summary>'sindri sandbox exec' (sandbox spec §3.6): a human-only CLI verb, not an API route. Usage and refusals exit 2, the command's exit code is the verb's.</summary>
[Collection(ProcessEnvironment.Name)]
public sealed class SandboxExecCliTests : IDisposable
{
    private readonly TempHome _home = new();
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();
    private readonly TextWriter _consoleOut = Console.Out;
    private readonly TextWriter _consoleError = Console.Error;
    private readonly RecordingExec _exec = new(7);

    public SandboxExecCliTests()
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

    private Func<string, string?> Environment(string? launch = null) => name => name switch
    {
        "AW_SINDRI_HOME" => _home.Path,
        "AW_SINDRI_LAUNCH" => launch,
        _ => null,
    };

    private Task<int> Run(Func<string, string?> environment, params string[] args) =>
        CommandTable.Run(["sandbox", "exec", .. args], environment, TimeProvider.System, services => services.AddSingleton<ISandboxExec>(_exec));

    private string PlanDirectory()
    {
        var directory = Path.Combine(_home.Path, "area");
        _ = Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, "sandbox-plan.json"), AwJson.Serialize(SandboxPlannerTests.Build(Req.WriteScope), AwJson.CreateOptions(SindriJsonContext.Default)));
        return directory;
    }

    private void HumanToken()
    {
        _ = Directory.CreateDirectory(Path.Combine(_home.Path, "auth"));
        File.WriteAllText(Path.Combine(_home.Path, "auth", "human.token"), "a-human-token\n");
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Without_a_launch_it_is_a_one_line_usage_error_with_exit_2()
    {
        var exit = await Run(Environment());

        Assert.Equal(2, exit);
        Assert.StartsWith("SND-CLI-003 ", _error.ToString(), StringComparison.Ordinal);
        Assert.Contains(SandboxExecVerb.Usage, _error.ToString(), StringComparison.Ordinal);
        Assert.Null(_exec.Command);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_session_Sindri_launched_is_refused_and_nothing_runs()
    {
        HumanToken();   // a launched session never calls as a human, whatever it can read

        var exit = await Run(Environment(launch: Req.WriteScope.Launch.Value), PlanDirectory());

        Assert.Equal(2, exit);
        Assert.StartsWith("SND-AUTH-002 ", _error.ToString(), StringComparison.Ordinal);
        Assert.Null(_exec.Command);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Without_the_per_start_token_it_is_refused()
    {
        var exit = await Run(Environment(), PlanDirectory());

        Assert.Equal(2, exit);
        Assert.StartsWith("SND-AUTH-002 ", _error.ToString(), StringComparison.Ordinal);
        Assert.Null(_exec.Command);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_exit_code_is_the_commands_and_the_words_after_the_dashes_are_the_command()
    {
        HumanToken();

        var exit = await Run(Environment(), PlanDirectory(), "--", "dotnet", "build", "--no-restore");

        Assert.Equal(7, exit);
        Assert.Equal(["dotnet", "build", "--no-restore"], _exec.Command);
        Assert.Equal(Req.WriteScope.Launch, _exec.Stored?.Launch);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task With_no_command_it_starts_a_shell_and_json_reports_the_exit_code()
    {
        HumanToken();

        var exit = await Run(Environment(), PlanDirectory(), "--json");

        Assert.Equal(7, exit);
        Assert.Equal(["/bin/sh"], _exec.Command);
        using var document = System.Text.Json.JsonDocument.Parse(_out.ToString());
        Assert.Equal(7, document.RootElement.GetProperty("exitCode").GetInt32());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_launch_with_no_stored_plan_is_a_one_line_error_with_exit_2()
    {
        HumanToken();

        var exit = await Run(Environment(), "LCH-" + new string('a', 32));

        Assert.Equal(2, exit);
        Assert.StartsWith("SND-CLI-003 ", _error.ToString(), StringComparison.Ordinal);
        _ = Assert.Single(_error.ToString().TrimEnd().Split('\n'));
        Assert.Null(_exec.Command);
    }

    private sealed class RecordingExec(int exit) : ISandboxExec
    {
        public IReadOnlyList<string>? Command { get; private set; }

        public SandboxPlan? Stored { get; private set; }

        public string PlanFileOf(LaunchId launch) => Path.Combine("/nonexistent", launch.Value, "sandbox-plan.json");

        public Task<Result<int>> Run(SandboxPlan stored, IReadOnlyList<string> command, CancellationToken cancellationToken)
        {
            Stored = stored;
            Command = command;
            return Task.FromResult(Results.Success(exit));
        }
    }
}
