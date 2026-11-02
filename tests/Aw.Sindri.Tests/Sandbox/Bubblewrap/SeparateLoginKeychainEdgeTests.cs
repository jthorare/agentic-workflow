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

    [Fact]
    public async Task An_expired_file_and_a_fresh_keychain_item_is_ready_and_prepare_rewrites_the_file()
    {
        using var home = new FakeSeparateHome(LoginState.Expired);
        var json = Login(FakeSeparateHome.Now + TimeSpan.FromHours(5));
        var source = home.Source(new FakeKeychain(SecurityCliKeychainLogin.ServiceFor(home.ClaudeHome), json));

        var status = source.Probe(_claude);
        var prepared = await source.Prepare(_claude, _launch, TestContext.Current.CancellationToken);

        Assert.Equal(LoginState.Ready, status.State);
        Assert.True(prepared.IsSuccess);
        var file = home.ClaudeHome + "/.credentials.json";
        Assert.Equal(json, await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken));
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(file));
        }
    }

    [Fact]
    public void A_ready_file_wins_without_asking_the_keychain()
    {
        using var home = new FakeSeparateHome(LoginState.Ready);
        var processes = new FakeProcesses().When(static (p, _) => p == "security", FakeProcesses.Ok(Login(FakeSeparateHome.Now + TimeSpan.FromHours(5))));
        var source = home.Source(new SecurityCliKeychainLogin(processes, isMac: true));

        Assert.Equal(LoginState.Ready, source.Probe(_claude).State);
        Assert.Empty(processes.Calls);
    }

    [Fact]
    public async Task An_expired_file_and_an_expired_keychain_item_stays_expired()
    {
        using var home = new FakeSeparateHome(LoginState.Expired);
        var item = JsonSerializer.Serialize(new { claudeAiOauth = new { accessToken = "kc", expiresAt = (FakeSeparateHome.Now - TimeSpan.FromMinutes(1)).ToUnixTimeMilliseconds() } });
        var source = home.Source(new FakeKeychain(SecurityCliKeychainLogin.ServiceFor(home.ClaudeHome), item));
        var before = await File.ReadAllTextAsync(home.ClaudeHome + "/.credentials.json", TestContext.Current.CancellationToken);

        var prepared = await source.Prepare(_claude, _launch, TestContext.Current.CancellationToken);

        Assert.Equal(LoginState.Expired, source.Probe(_claude).State);
        Assert.True(prepared.IsError);
        Assert.Equal(before, await File.ReadAllTextAsync(home.ClaudeHome + "/.credentials.json", TestContext.Current.CancellationToken));
    }

    private sealed class FakeKeychain(string service, string json) : IKeychainLogin
    {
        public string? Read(string requested) => requested == service ? json : null;
    }
}
