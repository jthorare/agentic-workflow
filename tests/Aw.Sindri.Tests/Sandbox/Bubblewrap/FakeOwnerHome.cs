using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Sandbox.Credentials;
using Aw.Sindri.Infrastructure.Sandbox;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Sandbox.Bubblewrap;

/// <summary>The fixture homes the owner-copy source reads: a fake owner home with a Claude login in the state the test names.</summary>
internal sealed class FakeOwnerHome : IDisposable
{
    internal const string McpToken = "ghp_ownersecret";

    internal static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    internal FakeOwnerHome(LoginState state, TimeSpan? lifetime = null, int codexRefreshedDaysAgo = 1)
    {
        Root = Path.Combine(Path.GetTempPath(), "aw-owner-copy-" + RealTime.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Root + "/.claude/hooks");
        Directory.CreateDirectory(Root + "/.claude/projects/-x");
        Directory.CreateDirectory(Root + "/.codex/sessions");
        File.WriteAllText(Root + "/.claude/settings.json", "{}");
        File.WriteAllText(Root + "/.claude/statusline.sh", "#!/bin/sh");
        File.WriteAllText(Root + "/.claude/.last-cleanup", "1");
        File.WriteAllText(Root + "/.claude/mcp-needs-auth-cache.json", "{}");
        File.WriteAllText(Root + "/.claude.json", ClaudeJson);
        File.WriteAllText(Root + "/.codex/config.toml", "model = \"x\"");
        if (state != LoginState.Missing)
        {
            var expires = Now + (state == LoginState.Expired ? TimeSpan.FromMinutes(-1) : lifetime ?? TimeSpan.FromHours(8));
            File.WriteAllText(Root + "/.claude/.credentials.json", JsonSerializer.Serialize(new { claudeAiOauth = new { accessToken = "a", refreshToken = "r", expiresAt = expires.ToUnixTimeMilliseconds() } }));
            CodexAuth($"{{\"tokens\":{{\"refresh_token\":\"r\"}},\"last_refresh\":\"{Now - TimeSpan.FromDays(codexRefreshedDaysAgo):O}\"}}");
        }
    }

    internal string Root { get; }

    internal string SindriHome => Root + "/sindri";

    internal FakeTimeProvider Clock { get; } = new(Now);

    // The owner's user-scope MCP servers carry whatever they were registered with (`claude mcp add --scope user --env KEY=VALUE`).
    private static string ClaudeJson =>
        "{\"onboarded\":true,\"oauthAccount\":{\"emailAddress\":\"owner@example.com\"},"
        + $"\"mcpServers\":{{\"github\":{{\"command\":\"npx\",\"env\":{{\"GITHUB_TOKEN\":\"{McpToken}\"}}}},\"api\":{{\"type\":\"http\",\"url\":\"https://x.example\",\"headers\":{{\"Authorization\":\"Bearer ownerbearer\"}}}}}},"
        + "\"projects\":{\"/r\":{\"mcpServers\":{\"p\":{\"env\":{\"K\":\"proj-secret\"}}}}}}";

    internal OwnerCopyCredentialSource Source() => new(new OwnerCopyOptions(Root, SindriHome), Clock);

    internal void CodexAuth(string json) => File.WriteAllText(Root + "/.codex/auth.json", json);

    public void Dispose() => Directory.Delete(Root, recursive: true);
}
