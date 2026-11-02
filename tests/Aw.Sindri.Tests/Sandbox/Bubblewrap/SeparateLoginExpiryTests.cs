using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox.Credentials;

namespace Aw.Sindri.Tests.Sandbox.Bubblewrap;

/// <summary>A separate login is used in place and may be refreshed by the session, so a refresh token keeps an old access token ready (spec §3.4, §15).</summary>
[Lane(TestLane.Integration)]
public sealed class SeparateLoginExpiryTests
{
    private static readonly long _past = (FakeSeparateHome.Now - TimeSpan.FromMinutes(1)).ToUnixTimeMilliseconds();
    private static readonly string _oldRefresh = FakeSeparateHome.Now.AddDays(-30).ToString("O");

    [Fact]
    public void Claude_access_token_past_expiry_is_ready_when_a_refresh_token_is_there()
    {
        using var home = new FakeSeparateHome(LoginState.Ready);
        File.WriteAllText(home.ClaudeHome + "/.credentials.json", string.Create(CultureInfo.InvariantCulture, $"{{\"claudeAiOauth\":{{\"accessToken\":\"a\",\"refreshToken\":\"r\",\"expiresAt\":{_past}}}}}"));

        var status = home.Source().Probe(AgentId.From("claude-code"));

        Assert.Equal(LoginState.Ready, status.State);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(_past), status.ExpiresAt);
    }

    [Fact]
    public void Claude_access_token_past_expiry_without_a_refresh_token_is_expired()
    {
        using var home = new FakeSeparateHome(LoginState.Ready);
        File.WriteAllText(home.ClaudeHome + "/.credentials.json", string.Create(CultureInfo.InvariantCulture, $"{{\"claudeAiOauth\":{{\"accessToken\":\"a\",\"expiresAt\":{_past}}}}}"));

        Assert.Equal(LoginState.Expired, home.Source().Probe(AgentId.From("claude-code")).State);
    }

    [Fact]
    public void Codex_login_past_its_refresh_age_is_ready_when_a_refresh_token_is_there()
    {
        using var home = new FakeSeparateHome(LoginState.Ready);
        File.WriteAllText(home.CodexHome + "/auth.json", $"{{\"tokens\":{{\"refresh_token\":\"r\"}},\"last_refresh\":\"{_oldRefresh}\"}}");

        Assert.Equal(LoginState.Ready, home.Source().Probe(AgentId.From("codex")).State);
    }

    [Fact]
    public void Codex_login_past_its_refresh_age_without_a_refresh_token_is_expired()
    {
        using var home = new FakeSeparateHome(LoginState.Ready);
        File.WriteAllText(home.CodexHome + "/auth.json", $"{{\"tokens\":{{}},\"last_refresh\":\"{_oldRefresh}\"}}");

        Assert.Equal(LoginState.Expired, home.Source().Probe(AgentId.From("codex")).State);
    }
}
