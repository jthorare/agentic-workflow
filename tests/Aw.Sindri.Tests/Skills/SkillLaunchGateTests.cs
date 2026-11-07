using System.Security.Cryptography;
using System.Text;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Skills;
using Aw.Sindri.Infrastructure.Skills;
using Aw.Sindri.Testing.Skills;
using Aw.Sindri.Tests.Sandbox.Egress;

namespace Aw.Sindri.Tests.Skills;

/// <summary>The two checks before a skill session starts (skill flows spec §3.1 requires.yaml, SND-SKILL-005; and the agent running the loaded SKILL.md, SND-SKILL-007): a real skills folder and a real agent home in temp directories.</summary>
[Lane(TestLane.Unit)]
public sealed class SkillLaunchGateTests : IDisposable
{
    private const string SkillMarkdown = "# review\n";

    private readonly TempDirectory _root = new();
    private readonly string _bin;
    private readonly string _agentHome;
    private readonly string _toolkit;

    public SkillLaunchGateTests()
    {
        _bin = Path.Combine(_root.Path, "bin");
        _agentHome = Path.Combine(_root.Path, "home", ".claude");
        _toolkit = Path.Combine(_root.Path, "toolkit", "skills");
        _ = Directory.CreateDirectory(_bin);
        _ = Directory.CreateDirectory(_agentHome);
        _ = Directory.CreateDirectory(Path.Combine(_toolkit, "review"));
        File.WriteAllText(Path.Combine(_toolkit, "review", "SKILL.md"), SkillMarkdown);
        File.WriteAllText(Path.Combine(_toolkit, "review", "block.yaml"), "schema: 1\n");
    }

    public void Dispose() => _root.Dispose();

    private static string Sha(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private SkillLaunch Launch(string? sha = null, string? trial = null) =>
        new("review", SkillTarget.ClaudeCode, SkillFixtures.Review, new Dictionary<string, string>(StringComparer.Ordinal) { ["pr"] = "42" })
        {
            ManifestPath = Path.Combine(_toolkit, "review", "block.yaml"),
            SkillMdSha = sha ?? Sha(SkillMarkdown),
            TrialSkillsDirectory = trial,
        };

    private FileAgentHome Home() => new(_agentHome, SkillTarget.ClaudeCode, Path.Combine(_root.Path, "home"), _bin);

    private void Bin(string name, bool executable)
    {
        var path = Path.Combine(_bin, name);
        File.WriteAllText(path, string.Empty);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, executable ? UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute : UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    private void Requires(string text) => File.WriteAllText(Path.Combine(_toolkit, "review", "requires.yaml"), text);

    private void Link(string markdown)
    {
        var folder = Path.Combine(_agentHome, "skills", "review");
        _ = Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "SKILL.md"), markdown);
    }

    [Fact]
    public void A_skill_that_needs_nothing_and_whose_agent_copy_matches_may_start()
    {
        Requires("{}\n");
        Link(SkillMarkdown);

        Assert.Null(SkillLaunchGate.Check(Launch(), Home()));
    }

    [Fact]
    public void A_missing_bin_mcp_server_skill_and_agent_each_fail_the_launch_and_name_their_fix_SND_SKILL_005()
    {
        Requires("bins: [gh]\nmcp: [prism-mcp]\nskills: [formal-verify]\nagents: [explorer]\n");
        Link(SkillMarkdown);

        var error = SkillLaunchGate.Check(Launch(), Home());

        Assert.Equal(SindriErrors.SkillRequirementMissing.Code, error?.Code);
        Assert.Contains("bin gh", error?.Message, StringComparison.Ordinal);
        Assert.Contains("MCP server prism-mcp", error?.Message, StringComparison.Ordinal);
        Assert.Contains("skill formal-verify", error?.Message, StringComparison.Ordinal);
        Assert.Contains("agent explorer", error?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Everything_in_requires_present_passes()
    {
        Requires("bins: [gh]\nmcp: [prism-mcp]\nskills: [formal-verify]\nagents: [explorer]\n");
        Link(SkillMarkdown);
        Bin("gh", executable: true);
        File.WriteAllText(Path.Combine(_root.Path, "home", ".claude.json"), """{"mcpServers":{"prism-mcp":{}}}""");
        _ = Directory.CreateDirectory(Path.Combine(_agentHome, "skills", "formal-verify"));
        File.WriteAllText(Path.Combine(_agentHome, "skills", "formal-verify", "SKILL.md"), "x");
        _ = Directory.CreateDirectory(Path.Combine(_agentHome, "agents"));
        File.WriteAllText(Path.Combine(_agentHome, "agents", "explorer.md"), "x");

        Assert.Null(SkillLaunchGate.Check(Launch(), Home()));
    }

    [Fact]
    public void A_bin_on_PATH_without_an_execute_bit_is_missing_SND_SKILL_005()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Windows files have no execute bit");
        Requires("bins: [gh]\n");
        Link(SkillMarkdown);
        Bin("gh", executable: false);

        var error = SkillLaunchGate.Check(Launch(), Home());

        Assert.Equal(SindriErrors.SkillRequirementMissing.Code, error?.Code);
        Assert.Contains("bin gh", error?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_same_SKILL_md_sha_passes_and_a_different_one_fails_naming_both_shas_and_the_fix_SND_SKILL_007()
    {
        Requires("{}\n");
        Link("# an older review\n");

        var error = SkillLaunchGate.Check(Launch(), Home());

        Assert.Equal(SindriErrors.SkillAgentCopyDiffers.Code, error?.Code);
        Assert.Contains(Sha("# an older review\n"), error?.Message, StringComparison.Ordinal);
        Assert.Contains(Sha(SkillMarkdown), error?.Message, StringComparison.Ordinal);
        Assert.Contains("re-run setup.sh", error?.Message, StringComparison.Ordinal);
        Assert.Contains("that's on purpose", error?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_SKILL_md_the_agent_home_lacks_fails_SND_SKILL_007()
    {
        Requires("{}\n");

        var error = SkillLaunchGate.Check(Launch(), Home());

        Assert.Equal(SindriErrors.SkillAgentCopyDiffers.Code, error?.Code);
        Assert.Contains("is missing", error?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_trial_with_a_skills_folder_skips_the_agent_copy_check()
    {
        Requires("{}\n");

        Assert.Null(SkillLaunchGate.Check(Launch(trial: _toolkit), Home()));
    }

    [Fact]
    public void A_symlinked_skill_folder_is_resolved_and_hashed_by_its_content()
    {
        Requires("{}\n");
        _ = Directory.CreateDirectory(Path.Combine(_agentHome, "skills"));
        _ = Directory.CreateSymbolicLink(Path.Combine(_agentHome, "skills", "review"), Path.Combine(_toolkit, "review"));

        Assert.Null(SkillLaunchGate.Check(Launch(), Home()));
    }

    [Fact]
    public void A_codex_home_reads_agents_as_toml_and_mcp_servers_from_config_toml()
    {
        var codex = Path.Combine(_root.Path, "home", ".codex");
        _ = Directory.CreateDirectory(Path.Combine(codex, "agents"));
        File.WriteAllText(Path.Combine(codex, "agents", "explorer.toml"), "x");
        File.WriteAllText(Path.Combine(codex, "config.toml"), "[mcp_servers.prism-mcp]\ncommand = \"x\"\n");
        var home = new FileAgentHome(codex, SkillTarget.Codex, Path.Combine(_root.Path, "home"), _bin);

        Assert.True(home.HasAgent("explorer"));
        Assert.True(home.HasMcp("prism-mcp"));
        Assert.False(home.HasMcp("github"));
    }

    [Fact]
    public void A_requires_file_that_does_not_parse_fails_with_the_reason()
    {
        Requires("bins: gh\n");

        var error = SkillLaunchGate.Check(Launch(), Home());

        Assert.Equal(SindriErrors.SkillRequirementMissing.Code, error?.Code);
        Assert.Contains("unreadable line", error?.Message, StringComparison.Ordinal);
    }
}
