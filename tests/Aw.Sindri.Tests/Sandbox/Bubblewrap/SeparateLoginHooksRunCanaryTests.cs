using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Credentials;
using Aw.Sindri.Infrastructure.Sandbox;
using Aw.Sindri.Testing.Sandbox;

namespace Aw.Sindri.Tests.Sandbox.Bubblewrap;

/// <summary>
/// Issue #1602: under the separate-login source the sandbox binds only the agent home and points CLAUDE_CONFIG_DIR at it, so the hooks-run canary must run the hooks
/// 'sindri sandbox login' installed there, not look for them in '$HOME/.claude/hooks' (absent by design). A fixture home keeps the owner's own '~/.claude' out of it.
/// </summary>
[Lane(TestLane.Integration)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class SeparateLoginHooksRunCanaryTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AgenticWorkflow.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("The repository root was not found above " + AppContext.BaseDirectory);
    }

    // What 'providers/claude/install-hooks.sh --target-dir <home>' leaves: config/hooks/*.sh in <home>/hooks and config/lib/failopen.sh in <home>/hooks/lib.
    private static void InstallHooks(string agentHome)
    {
        var hooks = Path.Combine(agentHome, "hooks");
        _ = Directory.CreateDirectory(Path.Combine(hooks, "lib"));
        var mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
        foreach (var script in Directory.GetFiles(Path.Combine(RepoRoot(), "config", "hooks"), "*.sh", SearchOption.TopDirectoryOnly))
        {
            var target = Path.Combine(hooks, Path.GetFileName(script));
            File.Copy(script, target);
            File.SetUnixFileMode(target, mode);
        }

        var failopen = Path.Combine(hooks, "lib", "failopen.sh");
        File.Copy(Path.Combine(RepoRoot(), "config", "lib", "failopen.sh"), failopen);
        File.SetUnixFileMode(failopen, mode);
    }

    [Fact]
    public async Task Hooks_run_passes_with_the_hooks_sandbox_login_installed_in_the_agent_home()
    {
        Assert.SkipUnless(BubblewrapBox.Unavailable is null, BubblewrapBox.Unavailable ?? string.Empty);
        var ct = TestContext.Current.CancellationToken;
        var owner = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var installed = new DirectoryInfo(owner + "/.agentic-workflow/bin/cs/current");
        Assert.SkipUnless(installed.Exists && installed.ResolveLinkTarget(returnFinalTarget: true) is { Exists: true }, "this box has no installed C# build, which the hooks' judge round trip needs");

        // why: the hooks' judge round trip (judge brief get) needs a C# build, and SandboxBox wires its judge wrapper from <home>/.agentic-workflow/bin/cs/current; a fixture home has none, so it links the owner's build (read-only use) and nothing else of the owner's home.
        var home = Path.Combine(BubblewrapBox.FixtureParent, "home-" + RealTime.NewGuid().ToString("N")[..8]);
        _ = Directory.CreateDirectory(home + "/.agentic-workflow/bin/cs");
        _ = Directory.CreateSymbolicLink(home + "/.agentic-workflow/bin/cs/current", installed.ResolveLinkTarget(returnFinalTarget: true)!.FullName);
        using var box = SandboxBox.Create(BubblewrapBox.FixtureParent, home);
        try
        {
            var target = AgentId.From("claude-code");
            var agentHome = AgentHomes.For(box.SindriHome, target);
            _ = Directory.CreateDirectory(agentHome);
            var expires = RealTime.NowOffset.AddHours(8).ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
            await File.WriteAllTextAsync(agentHome + "/.credentials.json", $"{{\"claudeAiOauth\":{{\"accessToken\":\"fixture\",\"refreshToken\":\"fixture\",\"expiresAt\":{expires}}}}}", ct);
            InstallHooks(agentHome);
            var prepared = await new SeparateLoginCredentialSource(box.SindriHome, RealTime.Clock).Prepare(target, box.WriteScope().Launch, ct);
            Assert.True(prepared.IsSuccess, prepared.IsError ? prepared.Error.ToString() : null);

            var sandbox = BubblewrapBox.Create();
            var plan = sandbox.Plan(box.WriteScope() with { Settings = new SandboxSettings(Login: new SandboxLogin(Source: ModelLoginSource.SeparateLogin)), Login = prepared.Value });
            Assert.True(plan.IsSuccess, plan.IsError ? plan.Error.ToString() : null);

            var result = await sandbox.SelfCheck(plan.Value, ct);
            var failure = result.IsSuccess ? null : SandboxRefusal.ReasonOf(result.Error) + ": " + result.Error.Message;

            // SelfCheck stops at the first failure: net-deny runs after hooks-run, so a failure from it still proves the hooks ran.
            Assert.True(failure is null || failure.StartsWith("net-deny: ", StringComparison.Ordinal), failure);
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }
}
