using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox.Credentials;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Infrastructure.Sandbox;

namespace Aw.Sindri.Tests.Sandbox.Bubblewrap;

/// <summary>Claude Code's login on macOS lives in the Keychain item "Claude Code-credentials" (sandbox spec §3.1, SB-9c): read when the file is missing, copied like the file.</summary>
[Lane(TestLane.Integration)]
public sealed class OwnerCopyKeychainTests
{
    private static readonly LaunchId _launch = LaunchId.From("LCH-" + new string('f', 32));
    private static readonly AgentId _claude = AgentId.From("claude-code");

    private static string Login(DateTimeOffset expires) =>
        JsonSerializer.Serialize(new { claudeAiOauth = new { accessToken = "kc-access-token", refreshToken = "kc-refresh-token", expiresAt = expires.ToUnixTimeMilliseconds() } });

    [Fact]
    public async Task A_login_file_that_is_there_means_the_keychain_is_never_asked()
    {
        using var home = new FakeOwnerHome(LoginState.Ready);
        var keychain = new FakeKeychain(Login(FakeOwnerHome.Now + TimeSpan.FromHours(5)));

        var prepared = await home.Source(keychain).Prepare(_claude, _launch, TestContext.Current.CancellationToken);

        Assert.True(prepared.IsSuccess);
        Assert.Equal(0, keychain.Reads);
    }

    [Fact]
    public async Task A_missing_file_on_a_mac_copies_the_keychain_login_as_a_0600_file_and_freshness_reads_it()
    {
        using var home = new FakeOwnerHome(LoginState.Missing);
        var json = Login(FakeOwnerHome.Now + TimeSpan.FromHours(5));
        var source = home.Source(new FakeKeychain(json));

        var status = source.Probe(_claude);
        var prepared = (await source.Prepare(_claude, _launch, TestContext.Current.CancellationToken)).Value;

        Assert.Equal((LoginState.Ready, FakeOwnerHome.Now + TimeSpan.FromHours(5)), (status.State, status.ExpiresAt));
        var copy = prepared.Handle.Value + "/.credentials.json";
        Assert.Equal(json, await File.ReadAllTextAsync(copy, TestContext.Current.CancellationToken));
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(copy));
        }

        Assert.False(File.Exists(home.Root + "/.claude/.credentials.json"));
    }

    [Fact]
    public void An_expired_keychain_login_is_expired()
    {
        using var home = new FakeOwnerHome(LoginState.Missing);

        var status = home.Source(new FakeKeychain(Login(FakeOwnerHome.Now - TimeSpan.FromMinutes(1)))).Probe(_claude);

        Assert.Equal(LoginState.Expired, status.State);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not json at all kc-secret-garbage")]
    [InlineData("[1,2]")]
    public async Task A_keychain_with_nothing_usable_refuses_like_a_missing_file_and_never_echoes_it(string? output)
    {
        using var home = new FakeOwnerHome(LoginState.Missing);
        using var empty = new FakeOwnerHome(LoginState.Missing);
        var viaKeychain = new SecurityCliKeychainLogin(new FakeProcesses().When(static (p, _) => p == "security", output is null ? FakeProcesses.Failed("not found", 44) : FakeProcesses.Ok(output)), isMac: true);

        var refused = await home.Source(viaKeychain).Prepare(_claude, _launch, TestContext.Current.CancellationToken);
        var baseline = await empty.Source().Prepare(_claude, _launch, TestContext.Current.CancellationToken);

        Assert.True(refused.IsError);
        Assert.Equal(baseline.Error.Code, refused.Error.Code);
        Assert.DoesNotContain("kc-secret-garbage", refused.Error.Message, StringComparison.Ordinal);
        Assert.Equal(baseline.Error.Message.Replace(empty.Root, "", StringComparison.Ordinal), refused.Error.Message.Replace(home.Root, "", StringComparison.Ordinal));
    }

    [Fact]
    public void The_security_command_gets_the_item_name_and_the_output_is_trimmed_json()
    {
        var processes = new FakeProcesses().When(static (p, _) => p == "security", FakeProcesses.Ok("{\"a\":1}\n"));

        var json = new SecurityCliKeychainLogin(processes, isMac: true).Read();

        Assert.Equal("{\"a\":1}", json);
        Assert.Equal(["find-generic-password", "-s", "Claude Code-credentials", "-w"], processes.Calls.Single().Arguments);
    }

    [Fact]
    public void Off_a_mac_the_security_command_is_never_run()
    {
        var processes = new FakeProcesses().When(static (_, _) => true, FakeProcesses.Ok("{\"a\":1}"));

        var json = new SecurityCliKeychainLogin(processes, isMac: false).Read();

        Assert.Null(json);
        Assert.Empty(processes.Calls);
    }

    private sealed class FakeKeychain(string? json) : IKeychainLogin
    {
        internal int Reads { get; private set; }

        public string? Read()
        {
            Reads++;
            return json;
        }
    }
}
