using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox.Credentials;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Infrastructure.Sandbox;

namespace Aw.Sindri.Tests.Sandbox.Bubblewrap;

/// <summary>
/// The edges of the separate-login Keychain read (#1928): off macOS the Keychain is never asked, and an item that is not a usable login is refused
/// without leaving a login file in the agent home.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class SeparateLoginKeychainEdgeTests
{
    private static readonly LaunchId _launch = LaunchId.From("LCH-" + new string('e', 32));
    private static readonly AgentId _claude = AgentId.From("claude-code");

    private static string Login(DateTimeOffset expires) =>
        JsonSerializer.Serialize(new { claudeAiOauth = new { accessToken = "kc-access-token", refreshToken = "kc-refresh-token", expiresAt = expires.ToUnixTimeMilliseconds() } });

    [Fact]
    public void Off_macOS_a_claude_home_with_no_file_is_still_missing()
    {
        using var home = new FakeSeparateHome(LoginState.Missing);
        var processes = new FakeProcesses().When(static (p, _) => p == "security", FakeProcesses.Ok(Login(FakeSeparateHome.Now + TimeSpan.FromHours(5))));
        var source = home.Source(new SecurityCliKeychainLogin(processes, isMac: false));

        var status = source.Probe(_claude);

        Assert.Equal(LoginState.Missing, status.State);
        Assert.StartsWith("no Claude Code login in ", status.Detail, StringComparison.Ordinal);
        Assert.Empty(processes.Calls);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    public async Task Prepare_refuses_an_unusable_keychain_item_and_writes_nothing(string output)
    {
        using var home = new FakeSeparateHome(LoginState.Missing);
        var processes = new FakeProcesses().When(static (p, _) => p == "security", FakeProcesses.Ok(output));
        var source = home.Source(new SecurityCliKeychainLogin(processes, isMac: true));

        var refused = await source.Prepare(_claude, _launch, TestContext.Current.CancellationToken);

        Assert.True(refused.IsError);
        Assert.False(File.Exists(home.ClaudeHome + "/.credentials.json"));
    }
}
