using Aw.Kernel.Testing;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Credentials;
using Aw.Sindri.Core.Skills;
using Aw.Sindri.Infrastructure.Sandbox.Launch;

namespace Aw.Sindri.Tests.Sandbox.Launch;

/// <summary>
/// Which agent home a skill launch's SND-SKILL-005/007 checks read on the host (skill flows spec §3.4): a separate login's own folder (bound at its own path), else the owner's.
/// The owner copy's CLAUDE_CONFIG_DIR ('~/.claude-run') is a sandbox path with no folder on the host (found in F-1's hand test: every owner-copy skill launch was refused).
/// </summary>
[Lane(TestLane.Unit)]
public sealed class SkillAgentHomeTests
{
    private const string Home = "/home/owner";

    private static PreparedLogin Login(ModelLoginSource source, string variable, string value) =>
        new(source, [], new Dictionary<string, string>(StringComparer.Ordinal) { [variable] = value }, [], new LoginHandle("test"));

    [Fact]
    public void An_owner_copy_Claude_launch_reads_the_owner_s_claude_folder_not_the_sandbox_path() =>
        Assert.Equal("/home/owner/.claude", SandboxLaunchStages.AgentHomeFolder(Login(ModelLoginSource.OwnerCopy, "CLAUDE_CONFIG_DIR", "/home/owner/.claude-run"), SkillTarget.ClaudeCode, Home));

    [Fact]
    public void An_owner_copy_Codex_launch_reads_the_owner_s_codex_folder() =>
        Assert.Equal("/home/owner/.codex", SandboxLaunchStages.AgentHomeFolder(Login(ModelLoginSource.OwnerCopy, "CODEX_HOME", "/home/owner/.codex-run"), SkillTarget.Codex, Home));

    [Fact]
    public void A_separate_login_reads_its_own_folder() =>
        Assert.Equal("/sindri/agent-homes/claude-code", SandboxLaunchStages.AgentHomeFolder(Login(ModelLoginSource.SeparateLogin, "CLAUDE_CONFIG_DIR", "/sindri/agent-homes/claude-code"), SkillTarget.ClaudeCode, Home));
}
