using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Credentials;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Infrastructure.Launch.Usage;
using Aw.Sindri.Infrastructure.Sandbox;
using Aw.Sindri.Infrastructure.Sessions;
using Aw.Sindri.Testing.Sandbox;
using Aw.Sindri.Tests.Launch;
using Aw.Sindri.Tests.Launch.Usage;

namespace Aw.Sindri.Tests.Sandbox.Bubblewrap;

/// <summary>
/// SB-8 proof (sandbox spec §12 SB-8, SB18): a Step whose login comes from the separate-login source runs in the real bubblewrap sandbox with none of the owner's
/// '~/.claude' bound, and what it writes in its agent home (a transcript, a session record) is found from outside by the usage reader and the session collector. The same script under the owner-copy login sees the owner's home, so the
/// difference is the profile's source and nothing else. Only existence is tested inside; nothing reads the owner's login. Heavy lane: it needs bwrap, socat and git.
/// </summary>
[Lane(TestLane.Heavy)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class SandboxSeparateLoginInsideTests
{
    // The owner's paths a separate login must not bind; each is printed as 'visible=<path>' when the sandbox can see it.
    private static readonly string[] _ownerPaths = [".claude", ".claude/projects", ".claude/settings.json", ".claude/.credentials.json", ".claude/hooks", ".claude.json", ".codex"];

    private static SandboxBox NewBox()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "the bubblewrap sandbox is Linux-only");
        Assert.SkipUnless(BubblewrapBox.Unavailable is null, BubblewrapBox.Unavailable ?? string.Empty);
        Assert.SkipUnless(SandboxBox.GitAvailable, "git is not installed");
        return SandboxBox.Create(BubblewrapBox.FixtureParent, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
    }

    [Fact]
    public async Task A_separate_login_Step_sees_none_of_the_owners_claude_home_and_writes_its_own_agent_home()
    {
        using var box = NewBox();
        Assert.SkipUnless(Directory.Exists(box.Home + "/.claude"), "the owner has no ~/.claude, so there is nothing to keep out");
        var login = await SeparateLogin(box);
        var agentHome = AgentHomes.For(box.SindriHome, AgentId.From("claude-code"));

        var transcript = UsageFixture.Claude("m-proof", "2026-10-28T08:00:00.000Z", "claude-sonnet-4-5", 7, 0, 0, 3);
        var record = "{ \"pid\": 4242, \"sessionId\": \"5b8e2c1a-7d34-4f0e-9a61-2c3d4e5f6a7b\", \"procStart\": \"1\", \"kind\": \"interactive\" }";
        var writes = "mkdir -p \"$CLAUDE_CONFIG_DIR/projects/-repo\" \"$CLAUDE_CONFIG_DIR/sessions\"\n"
            + $"printf '%s\\n' {Quote(transcript)} > \"$CLAUDE_CONFIG_DIR/projects/-repo/5b8e2c1a-7d34-4f0e-9a61-2c3d4e5f6a7b.jsonl\"\n"
            + $"printf '%s\\n' {Quote(record)} > \"$CLAUDE_CONFIG_DIR/sessions/4242.json\"\n";

        var ran = await RunInside(box, ModelLoginSource.SeparateLogin, login, writes + Probe());

        Assert.True(ran.Exit == 0, "the probe failed inside the sandbox: " + ran.Output);
        Assert.Contains("cfg=" + agentHome, ran.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("visible=", ran.Output, StringComparison.Ordinal);
        Assert.Contains("write=0", ran.Output, StringComparison.Ordinal);
        Assert.Equal("inside\n", await File.ReadAllTextAsync(agentHome + "/written-inside", TestContext.Current.CancellationToken));

        // The readers as the product registers them (owner's dirs plus the agent home's) find what the Step wrote; the owner's dirs here are empty ones in the box.
        var used = await new ClaudeCodeUsage(box.Root + "/owner-projects", [agentHome + "/projects"]).OfSession(SessionId.From("5b8e2c1a-7d34-4f0e-9a61-2c3d4e5f6a7b"), TestContext.Current.CancellationToken);
        Assert.Equal(new ModelTokens("claude-sonnet-4-5", 7, 0, 0, 3), Assert.Single(used));
        var records = new ClaudeSessionRecords(box.Root + "/owner-sessions", [agentHome + "/sessions"]).Read(TestContext.Current.CancellationToken);
        Assert.Equal("5b8e2c1a-7d34-4f0e-9a61-2c3d4e5f6a7b", Assert.Single(records.Value).Session?.Value);
        Assert.Empty(ProcessListing.Under(box.Root));
    }

    [Fact]
    public async Task The_same_Step_under_the_owner_copy_login_sees_the_owners_claude_home()
    {
        using var box = NewBox();
        Assert.SkipUnless(Directory.Exists(box.Home + "/.claude"), "the owner has no ~/.claude");

        var ran = await RunInside(box, ModelLoginSource.OwnerCopy, box.FakeLogin(), Probe());

        Assert.True(ran.Exit == 0, "the probe failed inside the sandbox: " + ran.Output);
        Assert.Contains("visible=.claude\n", ran.Output, StringComparison.Ordinal);
        Assert.Contains("cfg=" + box.Home + "/.claude-run", ran.Output, StringComparison.Ordinal);
        Assert.Empty(ProcessListing.Under(box.Root));
    }

    // A fake login in the fixture's agent home (never the owner's), prepared by the real source the profile key selects.
    private static async Task<PreparedLogin> SeparateLogin(SandboxBox box)
    {
        var target = AgentId.From("claude-code");
        var home = AgentHomes.For(box.SindriHome, target);
        Directory.CreateDirectory(home);
        var expires = RealTime.NowOffset.AddHours(8).ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        await File.WriteAllTextAsync(home + "/.credentials.json", $"{{\"claudeAiOauth\":{{\"accessToken\":\"fixture\",\"refreshToken\":\"fixture\",\"expiresAt\":{expires}}}}}", TestContext.Current.CancellationToken);

        var prepared = await new SeparateLoginCredentialSource(box.SindriHome, RealTime.Clock).Prepare(target, box.WriteScope().Launch, TestContext.Current.CancellationToken);
        Assert.True(prepared.IsSuccess, prepared.IsError ? prepared.Error.ToString() : null);
        return prepared.Value;
    }

    private static string Probe() =>
        "echo \"cfg=$CLAUDE_CONFIG_DIR\"\n"
        + string.Concat(_ownerPaths.Select(p => $"[ -e \"$HOME/{p}\" ] && echo 'visible={p}'\n"))
        + "echo inside > \"$CLAUDE_CONFIG_DIR/written-inside\"; echo \"write=$?\"\n"
        + "exit 0\n";

    // The profile key 'sandbox.login.source' is the only thing the two facts change (spec SB-8: switching it changes no other code).
    private static async Task<Ran> RunInside(SandboxBox box, ModelLoginSource source, PreparedLogin login, string script)
    {
        using var tmux = new PrivateTmux();
        var ct = TestContext.Current.CancellationToken;
        var sandbox = BubblewrapBox.Create();
        var plan = sandbox.Plan(box.WriteScope() with { Settings = new SandboxSettings(Login: new SandboxLogin(Source: source)), Login = login });
        Assert.True(plan.IsSuccess, plan.IsError ? plan.Error.ToString() : null);

        await using var egress = StartEgress(plan.Value);
        var wrapped = sandbox.Wrap(plan.Value, ["/bin/sh", "-c", script]);
        Assert.True(wrapped.IsSuccess, wrapped.IsError ? wrapped.Error.ToString() : null);

        var output = Path.Combine(box.Root, "inside.out");
        var code = Path.Combine(box.Root, "inside.code");
        var line = string.Join(' ', wrapped.Value.Select(Quote)) + $" > {Quote(output)} 2>&1; echo $? > {Quote(code)}";
        var run = Path.Combine(box.Root, "inside.sh");
        await File.WriteAllTextAsync(run, line, ct);   // why: a write-scope argv is longer than tmux's message limit (about 16 KB), so tmux runs it from a file
        Assert.NotNull(tmux.Run("new-session", "-d", "-s", "separate-login-inside", "-c", box.Root, "sh", run));
        for (var waited = 0; waited < 1200 && !File.Exists(code); waited++)
        {
            await Task.Delay(100, ct);
        }

        Assert.True(File.Exists(code), "the script did not finish inside the sandbox in 120 s");
        return new Ran(int.Parse((await File.ReadAllTextAsync(code, ct)).Trim(), System.Globalization.CultureInfo.InvariantCulture), await File.ReadAllTextAsync(output, ct));
    }

    private static RunningEgress StartEgress(SandboxPlan plan)
    {
        var network = new SystemEgressNetwork();
        var started = new EgressService(network, network, Environment.GetEnvironmentVariable, Path.Combine(plan.RunDirectory, "egress")).Start(plan.Network, EgressCaps.Default);
        Assert.True(started.IsSuccess, started.IsError ? started.Error.ToString() : null);
        return started.Value;
    }

    private static string Quote(string value) => "'" + value.Replace("'", "'\\''", StringComparison.Ordinal) + "'";

    private sealed record Ran(int Exit, string Output);
}
