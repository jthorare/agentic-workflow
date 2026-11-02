using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox.Credentials;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Infrastructure.Sandbox;

namespace Aw.Sindri.Tests.Sandbox.Bubblewrap;

/// <summary>
/// A separate-login Claude Code on macOS keeps its login in the Keychain, in the item named after the agent home it was started with (#1928, sandbox spec §3.4, SB-9c),
/// so the source reads that item when the home has no login file, like the owner-copy source does for the owner's.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class SeparateLoginKeychainTests
{
    private static readonly LaunchId _launch = LaunchId.From("LCH-" + new string('f', 32));
    private static readonly AgentId _claude = AgentId.From("claude-code");
    private static readonly AgentId _codex = AgentId.From("codex");

    private static string Login(DateTimeOffset expires, bool refresh = true) =>
        JsonSerializer.Serialize(new { claudeAiOauth = new { accessToken = "kc-access-token", refreshToken = refresh ? "kc-refresh-token" : null, expiresAt = expires.ToUnixTimeMilliseconds() } });

    // The service name worked out here on its own, so the test does not trust the code under test to say what it is.
    private static string ServiceNamedAfter(string configDirectory) =>
        "Claude Code-credentials-" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(configDirectory.Normalize(NormalizationForm.FormC))))[..8];

    [Fact]
    public async Task A_mac_login_in_the_keychain_for_the_agent_home_is_ready_and_prepare_writes_it_0600_into_the_home()
    {
        using var home = new FakeSeparateHome(LoginState.Missing);
        var json = Login(FakeSeparateHome.Now + TimeSpan.FromHours(5));
        var source = home.Source(new FakeKeychain(ServiceNamedAfter(home.ClaudeHome), json));

        var status = source.Probe(_claude);
        var prepared = await source.Prepare(_claude, _launch, TestContext.Current.CancellationToken);

        Assert.Equal((LoginState.Ready, FakeSeparateHome.Now + TimeSpan.FromHours(5)), (status.State, status.ExpiresAt));
        Assert.True(prepared.IsSuccess);
        var file = home.ClaudeHome + "/.credentials.json";
        Assert.Equal(json, await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken));
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(file));
        }
    }

    [Fact]
    public void The_keychain_is_asked_for_the_item_named_after_the_agent_home()
    {
        using var home = new FakeSeparateHome(LoginState.Missing);

        Assert.Equal(ServiceNamedAfter(home.ClaudeHome), SecurityCliKeychainLogin.ServiceFor(home.ClaudeHome));
        Assert.Equal("Claude Code-credentials", SecurityCliKeychainLogin.ServiceFor(configDirectory: null));
    }

    [Fact]
    public async Task A_login_file_in_the_agent_home_means_the_keychain_is_never_asked()
    {
        using var home = new FakeSeparateHome(LoginState.Ready);
        var keychain = new FakeKeychain(ServiceNamedAfter(home.ClaudeHome), Login(FakeSeparateHome.Now + TimeSpan.FromHours(5)));
        var source = home.Source(keychain);

        var status = source.Probe(_claude);
        var prepared = await source.Prepare(_claude, _launch, TestContext.Current.CancellationToken);

        Assert.Equal(LoginState.Ready, status.State);
        Assert.True(prepared.IsSuccess);
        Assert.Empty(keychain.Asked);
    }

    [Fact]
    public void An_expired_keychain_login_without_a_refresh_token_is_expired()
    {
        using var home = new FakeSeparateHome(LoginState.Missing);
        var keychain = new FakeKeychain(ServiceNamedAfter(home.ClaudeHome), Login(FakeSeparateHome.Now - TimeSpan.FromMinutes(1), refresh: false));

        Assert.Equal(LoginState.Expired, home.Source(keychain).Probe(_claude).State);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not json kc-secret-garbage")]
    [InlineData("[1,2]")]
    public async Task A_keychain_with_nothing_usable_refuses_like_a_missing_file_and_never_echoes_it(string? output)
    {
        using var home = new FakeSeparateHome(LoginState.Missing);
        using var empty = new FakeSeparateHome(LoginState.Missing);
        var viaKeychain = new SecurityCliKeychainLogin(new FakeProcesses().When(static (p, _) => p == "security", output is null ? FakeProcesses.Failed("not found", 44) : FakeProcesses.Ok(output)), isMac: true);

        var refused = await home.Source(viaKeychain).Prepare(_claude, _launch, TestContext.Current.CancellationToken);
        var baseline = await empty.Source().Prepare(_claude, _launch, TestContext.Current.CancellationToken);

        Assert.True(refused.IsError);
        Assert.Equal(baseline.Error.Code, refused.Error.Code);
        Assert.DoesNotContain("kc-secret-garbage", refused.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Codex_never_asks_the_keychain()
    {
        using var home = new FakeSeparateHome(LoginState.Missing);
        var keychain = new FakeKeychain(ServiceNamedAfter(home.ClaudeHome), Login(FakeSeparateHome.Now + TimeSpan.FromHours(5)));

        var status = home.Source(keychain).Probe(_codex);

        Assert.Equal(LoginState.Missing, status.State);
        Assert.Empty(keychain.Asked);
    }

    private sealed class FakeKeychain(string expectedService, string json) : IKeychainLogin
    {
        internal List<string> Asked { get; } = [];

        public string? Read(string service)
        {
            Asked.Add(service);
            return service == expectedService ? json : null;
        }
    }
}
