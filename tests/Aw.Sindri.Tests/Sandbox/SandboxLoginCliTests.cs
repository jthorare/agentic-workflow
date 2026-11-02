using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox.Credentials;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Tests.Ledger;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Sandbox;

/// <summary>'sindri sandbox login' (sandbox spec SB-8): a human-only CLI verb, not an API route. Usage and refusals exit 2, the sign-in's exit code is the verb's.</summary>
[Collection(ProcessEnvironment.Name), Trait("Lane", "integration")]
public sealed class SandboxLoginCliTests : IDisposable
{
    private readonly TempHome _home = new();
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();
    private readonly TextWriter _consoleOut = Console.Out;
    private readonly TextWriter _consoleError = Console.Error;
    private readonly RecordingLogin _login = new(5);

    public SandboxLoginCliTests()
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
        CommandTable.Run(["sandbox", "login", .. args], environment, TimeProvider.System, services => services.AddSingleton<ISandboxLogin>(_login));

    private void HumanToken()
    {
        _ = Directory.CreateDirectory(Path.Combine(_home.Path, "auth"));
        File.WriteAllText(Path.Combine(_home.Path, "auth", "human.token"), "a-human-token\n");
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Without_a_target_it_is_a_one_line_usage_error_with_exit_2()
    {
        HumanToken();

        var exit = await Run(Environment());

        Assert.Equal(2, exit);
        Assert.StartsWith("SND-CLI-003 ", _error.ToString(), StringComparison.Ordinal);
        Assert.Contains(SandboxLoginVerb.Usage, _error.ToString(), StringComparison.Ordinal);
        Assert.Null(_login.Target);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("cursor")]
    [InlineData("Not An Id")]
    public async Task An_unknown_target_is_a_usage_error_and_nothing_runs(string target)
    {
        HumanToken();

        var exit = await Run(Environment(), target);

        Assert.Equal(2, exit);
        Assert.StartsWith("SND-CLI-003 ", _error.ToString(), StringComparison.Ordinal);
        Assert.Null(_login.Target);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_session_Sindri_launched_is_refused_and_nothing_runs()
    {
        HumanToken();   // a launched session never calls as a human, whatever it can read

        var exit = await Run(Environment(launch: "LCH-" + new string('a', 32)), "claude-code");

        Assert.Equal(2, exit);
        Assert.StartsWith("SND-AUTH-002 ", _error.ToString(), StringComparison.Ordinal);
        Assert.Null(_login.Target);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Without_the_per_start_token_it_is_refused()
    {
        var exit = await Run(Environment(), "codex");

        Assert.Equal(2, exit);
        Assert.StartsWith("SND-AUTH-002 ", _error.ToString(), StringComparison.Ordinal);
        Assert.Null(_login.Target);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("claude-code")]
    [InlineData("codex")]
    public async Task The_exit_code_is_the_sign_ins_and_the_target_is_passed_on(string target)
    {
        HumanToken();

        var exit = await Run(Environment(), target);

        Assert.Equal(5, exit);
        Assert.Equal(AgentId.From(target), _login.Target);
    }

    private sealed class RecordingLogin(int exit) : ISandboxLogin
    {
        public AgentId? Target { get; private set; }

        public Task<Result<int>> Run(AgentId target, CancellationToken cancellationToken)
        {
            Target = target;
            return Task.FromResult(Results.Success(exit));
        }
    }
}
