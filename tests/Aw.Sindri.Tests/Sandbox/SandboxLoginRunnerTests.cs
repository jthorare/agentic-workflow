using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox.Credentials;
using Aw.Sindri.Infrastructure.Sandbox;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Sandbox;

/// <summary>'sindri sandbox login' behind the CLI (sandbox spec SB-8): the runner makes the agent home, runs the hook installer into it, then the sign-in, over fake installers and a fake claude and codex.</summary>
[Collection(ProcessEnvironment.Name)]
[Lane(TestLane.Integration)]
public sealed class SandboxLoginRunnerTests : IDisposable
{
    private readonly TempHome _home = new();
    private readonly string _toolkit;
    private readonly string _bin;
    private readonly string _log;
    private readonly StringWriter _error = new();
    private readonly TextWriter _consoleError = Console.Error;

    public SandboxLoginRunnerTests()
    {
        _toolkit = Path.Combine(_home.Path, "toolkit");
        _bin = Path.Combine(_home.Path, "bin");
        _log = Path.Combine(_home.Path, "calls.log");
        _ = Directory.CreateDirectory(_bin);
        Console.SetError(_error);
    }

    public void Dispose()
    {
        Console.SetError(_consoleError);
        _home.Dispose();
    }

    private string SindriHome => Path.Combine(_home.Path, "sindri");

    private SandboxLoginRunner Runner() => new(SindriHome, name => name == "TOOLKIT_DIR" ? _toolkit : null, _bin);

    private void Installer(string provider, string body)
    {
        var directory = Path.Combine(_toolkit, "providers", provider);
        _ = Directory.CreateDirectory(directory);
        _ = ExecutableScript.Write(Path.Combine(directory, "install-hooks.sh"), body + "\n", "/bin/bash");
    }

    private void RecordingInstaller(string provider, int exit = 0) =>
        Installer(provider, $"echo \"installer {provider} $* TOOLKIT_DIR=$TOOLKIT_DIR\" >> '{_log}'\nexit {exit.ToString(System.Globalization.CultureInfo.InvariantCulture)}");

    private void Agent(string name, string variable) =>
        _ = ExecutableScript.Write(Path.Combine(_bin, name), $"echo \"{name} $* {variable}=${variable}\" >> '{_log}'\n");

    private string[] Calls() => File.Exists(_log) ? File.ReadAllLines(_log) : [];

    [Theory]
    [InlineData("claude-code", "claude", "CLAUDE_CONFIG_DIR", "")]
    [InlineData("codex", "codex", "CODEX_HOME", "login")]
    public async Task The_home_is_made_the_installer_runs_into_it_and_then_the_sign_in(string target, string provider, string variable, string signInArgs)
    {
        RecordingInstaller(provider);
        Agent(provider, variable);
        var id = AgentId.From(target);
        var home = AgentHomes.For(SindriHome, id);

        var ran = await Runner().Run(id, CancellationToken.None);

        Assert.True(ran.IsSuccess);
        Assert.Equal(0, ran.Value);
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(home));
        }

        Assert.Equal([$"installer {provider} --target-dir {home} TOOLKIT_DIR={_toolkit}", $"{provider} {signInArgs} {variable}={home}"], Calls());
    }

    [Fact]
    public async Task Codex_prints_the_hooks_trust_line_and_claude_does_not()
    {
        RecordingInstaller("claude");
        RecordingInstaller("codex");
        Agent("claude", "CLAUDE_CONFIG_DIR");
        Agent("codex", "CODEX_HOME");

        _ = await Runner().Run(AgentId.From("claude-code"), CancellationToken.None);
        Assert.DoesNotContain("/hooks", _error.ToString(), StringComparison.Ordinal);

        _ = await Runner().Run(AgentId.From("codex"), CancellationToken.None);
        Assert.Contains("CODEX_HOME=" + AgentHomes.For(SindriHome, AgentId.From("codex")), _error.ToString(), StringComparison.Ordinal);
        Assert.Contains("/hooks", _error.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("claude-code", "claude", "CLAUDE_CONFIG_DIR")]
    [InlineData("codex", "codex", "CODEX_HOME")]
    public async Task A_failing_installer_stops_before_the_sign_in(string target, string provider, string variable)
    {
        RecordingInstaller(provider, exit: 3);
        Agent(provider, variable);

        var ran = await Runner().Run(AgentId.From(target), CancellationToken.None);

        Assert.True(ran.IsError);
        _ = Assert.Single(Calls());
        Assert.StartsWith("installer ", Calls()[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_missing_installer_is_an_error_and_nothing_runs()
    {
        Agent("claude", "CLAUDE_CONFIG_DIR");

        var ran = await Runner().Run(AgentId.From("claude-code"), CancellationToken.None);

        Assert.True(ran.IsError);
        Assert.Empty(Calls());
    }
}
