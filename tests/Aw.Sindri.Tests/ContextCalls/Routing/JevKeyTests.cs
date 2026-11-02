using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Processes;
using Aw.Sindri.Host.Models;

namespace Aw.Sindri.Tests.ContextCalls.Routing;

/// <summary>Whether Jev has a key (#1727): the variable, else on a Mac the keychain entry, asked without reading the secret.</summary>
public sealed class JevKeyTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static JevKey Key(string? variable, StubRunner runner, bool mac) => new(name => name == "TYPESAFE_API_KEY" ? variable : null, () => runner, mac);

    [Fact, Trait("Lane", "unit")]
    public async Task A_variable_that_is_set_is_a_key_without_running_the_tool()
    {
        var runner = new StubRunner(ProcessResult.Exited(exitCode: 1, standardOutput: "", standardError: ""));

        Assert.True(await Key("tsk_x", runner, mac: true).Present(Token));
        Assert.Empty(runner.Asked);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task On_a_mac_the_keychain_entry_decides_and_the_secret_is_never_read()
    {
        var found = new StubRunner(ProcessResult.Exited(exitCode: 0, standardOutput: "", standardError: ""));
        var missing = new StubRunner(ProcessResult.Exited(exitCode: 44, standardOutput: "", standardError: "could not be found"));

        Assert.True(await Key(variable: null, runner: found, mac: true).Present(Token));
        Assert.False(await Key(variable: null, runner: missing, mac: true).Present(Token));
        var asked = Assert.Single(found.Asked);
        Assert.Equal("security", asked.FileName);
        Assert.Equal(["find-generic-password", "-s", "agentic-workflow-typesafe", "-a", "TYPESAFE_API_KEY"], asked.Arguments, StringComparer.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(ProcessEnd.TimedOut)]
    [InlineData(ProcessEnd.NotStarted)]
    public async Task A_keychain_tool_that_hangs_or_is_not_there_reads_as_no_key(ProcessEnd end)
    {
        var runner = new StubRunner(new ProcessResult(end, ExitCode: null, StandardOutput: "", StandardError: ""));

        Assert.False(await Key(variable: null, runner: runner, mac: true).Present(Token));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Off_a_mac_there_is_no_keychain_to_ask()
    {
        var runner = new StubRunner(ProcessResult.Exited(exitCode: 0, standardOutput: "", standardError: ""));

        Assert.False(await Key(string.Empty, runner, mac: false).Present(Token));
        Assert.Empty(runner.Asked);
    }

    private sealed class StubRunner(ProcessResult result) : IProcessRunner
    {
        public List<ProcessRequest> Asked { get; } = [];

        public ValueTask<ProcessResult> Run(ProcessRequest request, CancellationToken cancellationToken)
        {
            Asked.Add(request);
            return ValueTask.FromResult(result);
        }
    }
}
