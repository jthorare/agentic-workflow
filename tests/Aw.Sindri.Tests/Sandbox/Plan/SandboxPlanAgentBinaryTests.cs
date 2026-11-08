using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Req = Aw.Sindri.Testing.Sandbox.SandboxRequests;

namespace Aw.Sindri.Tests.Sandbox.Plan;

/// <summary>#2149: an agent binary reached through a link in ~/.local/bin must resolve inside the sandbox, or the launch is refused before it starts.</summary>
public sealed class SandboxPlanAgentBinaryTests
{
    private const string ClaudeLink = Req.Home + "/.local/bin/claude";
    private const string ClaudeReal = Req.Home + "/.local/share/claude/versions/2.1.319";
    private const string CodexLink = Req.Home + "/.local/bin/codex";
    private const string CodexReal = Req.Home + "/.local/share/codex-cli/versions/0.9.0/bin/codex";

    private static SandboxRequest With(string agent, string link, string real, IReadOnlyList<string>? extraDeny = null) =>
        Req.WriteScope with
        {
            Target = AgentId.From(agent),
            ExtraDeny = extraDeny,
            Host = Req.Host with { BinaryLinks = new Dictionary<string, string>(StringComparer.Ordinal) { [link] = real } },
        };

    private static bool BindsReadOnly(SandboxPlan plan, string realPath) =>
        plan.Mounts.Any(m => m.Kind == PlanMountKind.ReadOnly && m.Source is null
            && (string.Equals(m.Path, realPath, StringComparison.Ordinal) || realPath.StartsWith(m.Path + "/", StringComparison.Ordinal))
            && m.Path.StartsWith(Req.Home + "/.local/share/", StringComparison.Ordinal));

    [Theory, Trait("Lane", "unit")]
    [InlineData("claude-code", ClaudeLink, ClaudeReal)]
    [InlineData("codex", CodexLink, CodexReal)]
    public void A_linked_agent_binary_has_its_install_tree_mounted_read_only(string agent, string link, string real)
    {
        var plan = SandboxPlannerTests.Build(With(agent, link, real));

        Assert.True(BindsReadOnly(plan, real), $"the link {link} dangles in the sandbox: nothing mounts its target {real}");
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("claude-code", ClaudeLink, ClaudeReal, Req.Home + "/.local/share/claude")]
    [InlineData("codex", CodexLink, CodexReal, Req.Home + "/.local/share/codex-cli")]
    public void A_linked_agent_binary_under_a_denied_tree_is_refused_as_bin_denied(string agent, string link, string real, string denied)
    {
        var result = SandboxPlanner.Build(SandboxMechanism.PassThrough, With(agent, link, real, [denied]));

        Assert.True(result.IsError, "the binary can't be reached and the launch was not refused");
        Assert.Equal(SindriErrors.LaunchUnconfinable.Code, result.Error.Code);
        Assert.Equal(SandboxReasons.BinDenied, SandboxRefusal.ReasonOf(result.Error));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_binary_outside_the_share_folder_binds_the_first_folder_under_the_home()
    {
        var tree = AgentBinaryTree.Of(Req.Home, Req.Home + "/.npm-global/lib/node_modules/claude/bin/claude");

        Assert.Equal(Req.Home + "/.npm-global", tree);
    }
}
