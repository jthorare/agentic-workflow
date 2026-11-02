using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox.Credentials;
using Aw.Sindri.Infrastructure.Sandbox;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Sandbox.Bubblewrap;

/// <summary>A fake Sindri home whose agent homes hold the separate logins in the state the test names.</summary>
internal sealed class FakeSeparateHome : IDisposable
{
    internal FakeSeparateHome(LoginState state)
    {
        SindriHome = Path.Combine(Path.GetTempPath(), "aw-separate-login-" + RealTime.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(ClaudeHome);
        Directory.CreateDirectory(CodexHome);
        if (state == LoginState.Missing)
        {
            return;
        }

        var expires = Now + (state == LoginState.Expired ? TimeSpan.FromMinutes(-1) : TimeSpan.FromHours(8));
        File.WriteAllText(ClaudeHome + "/.credentials.json", JsonSerializer.Serialize(new { claudeAiOauth = new { accessToken = "a", refreshToken = state == LoginState.Expired ? null : "r", expiresAt = expires.ToUnixTimeMilliseconds() } }));
        File.WriteAllText(ClaudeHome + "/.claude.json", "{\"onboarded\":true}");
        File.WriteAllText(CodexHome + "/auth.json", $"{{\"tokens\":{{{(state == LoginState.Expired ? string.Empty : "\"refresh_token\":\"r\"")}}},\"last_refresh\":\"{Now - TimeSpan.FromDays(state == LoginState.Expired ? 30 : 1):O}\"}}");
    }

    internal static DateTimeOffset Now { get; } = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    internal string SindriHome { get; }

    internal string ClaudeHome => AgentHomes.For(SindriHome, AgentId.From("claude-code"));

    internal string CodexHome => AgentHomes.For(SindriHome, AgentId.From("codex"));

    internal FakeTimeProvider Clock { get; } = new(Now);

    internal SeparateLoginCredentialSource Source() => new(SindriHome, Clock);

    public void Dispose() => Directory.Delete(SindriHome, recursive: true);
}
