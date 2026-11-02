using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Credentials;
using Aw.Sindri.Testing.Sandbox;

namespace Aw.Sindri.Tests.Sandbox.Bubblewrap;

/// <summary>The separate-login source passes the credential-source contract every adapter passes (sandbox spec SB18, SB-8).</summary>
[Lane(TestLane.Integration)]
public sealed class SeparateLoginCredentialSourceContractTests : IModelCredentialSourceContract
{
    protected override IModelCredentialSource Create(LoginState state) => new FakeSeparateHome(state).Source();

    /// <summary>The agent home is bound once, read-write, at its own path, and the CLI is pointed at it.</summary>
    /// <returns>The test.</returns>
    [Theory]
    [InlineData("claude-code", "CLAUDE_CONFIG_DIR")]
    [InlineData("codex", "CODEX_HOME")]
    public async Task Prepare_binds_the_agent_home_at_its_own_path_and_points_the_cli_at_it(string agent, string variable)
    {
        using var home = new FakeSeparateHome(LoginState.Ready);
        var target = AgentId.From(agent);
        var expected = AgentHomes.For(home.SindriHome, target);

        var prepared = (await home.Source().Prepare(target, Core.Workflow.Engine.LaunchId.From("LCH-" + new string('c', 32)), TestContext.Current.CancellationToken)).Value;

        Assert.Equal(new LoginMount(expected, expected, MountMode.ReadWrite), prepared.Mounts.Single());
        Assert.Equal(expected, prepared.Environment[variable]);
        Assert.Equal(ModelLoginSource.SeparateLogin, prepared.Source);
    }

    /// <summary>A target with no login file in its home is no-login, and a home that does not exist is too.</summary>
    [Fact]
    public void A_missing_agent_home_or_login_file_is_missing()
    {
        using var home = new FakeSeparateHome(LoginState.Ready);
        Directory.Delete(home.CodexHome, recursive: true);
        File.Delete(home.ClaudeHome + "/.credentials.json");

        Assert.Equal(LoginState.Missing, home.Source().Probe(AgentId.From("codex")).State);
        Assert.Equal(LoginState.Missing, home.Source().Probe(AgentId.From("claude-code")).State);
    }

    /// <summary>Release leaves the home alone: it is the target's own login, not a copy.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Release_leaves_the_agent_home_in_place()
    {
        using var home = new FakeSeparateHome(LoginState.Ready);
        var source = home.Source();

        var prepared = (await source.Prepare(AgentId.From("claude-code"), Core.Workflow.Engine.LaunchId.From("LCH-" + new string('c', 32)), TestContext.Current.CancellationToken)).Value;
        source.Release(prepared.Handle);

        Assert.True(File.Exists(home.ClaudeHome + "/.credentials.json"));
    }
}
