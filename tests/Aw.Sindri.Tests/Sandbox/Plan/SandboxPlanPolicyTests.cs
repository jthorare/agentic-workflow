using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Req = Aw.Sindri.Testing.Sandbox.SandboxRequests;

namespace Aw.Sindri.Tests.Sandbox.Plan;

/// <summary>The inner policy, hooks and the attended differences of a plan (sandbox spec §5, §3.9).</summary>
public sealed class SandboxPlanPolicyTests
{
    private static bool Has(SandboxPlan plan, InnerRuleEffect effect, string tool, Func<string, bool> pattern) =>
        plan.Policy.Rules.Any(r => r.Effect == effect && string.Equals(r.Tool, tool, StringComparison.Ordinal) && pattern(r.Pattern));

    [Fact, Trait("Lane", "unit")]
    public void A_step_refuses_and_denies_the_dangerous_calls_and_the_login_copy()
    {
        var plan = SandboxPlannerTests.Build(Req.WriteScope);

        Assert.Equal(InnerMode.Refuse, plan.Policy.Mode);
        Assert.True(Has(plan, InnerRuleEffect.Deny, "Bash", p => p == "git push:*"));
        Assert.True(Has(plan, InnerRuleEffect.Deny, "Bash", p => p == "gh:*"));
        Assert.True(Has(plan, InnerRuleEffect.Deny, "Bash", p => p == "env"));
        Assert.True(Has(plan, InnerRuleEffect.Deny, "Bash", p => p == "printenv*"));
        Assert.True(Has(plan, InnerRuleEffect.Deny, "Read", p => p.StartsWith(Req.Home + "/.claude-run", StringComparison.Ordinal)));
        Assert.True(Has(plan, InnerRuleEffect.Deny, "Read", p => p.StartsWith(Req.StateDir, StringComparison.Ordinal)));
        Assert.True(Has(plan, InnerRuleEffect.Deny, "Bash", p => p.StartsWith("rm", StringComparison.Ordinal) && p.Contains(Req.StateDir + "/locks", StringComparison.Ordinal)));
        Assert.True(Has(plan, InnerRuleEffect.Deny, "Bash", p => p.StartsWith("mv", StringComparison.Ordinal) && p.Contains(Req.StateDir + "/judge", StringComparison.Ordinal)));
        Assert.True(Has(plan, InnerRuleEffect.Deny, "Bash", p => p.StartsWith("rm -rf /", StringComparison.Ordinal)));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_rtk_forms_are_denied_too_while_rtk_rewrite_is_kept()
    {
        var plan = SandboxPlannerTests.Build(Req.WriteScope);
        var withoutRtk = SandboxPlannerTests.Build(Req.WriteScope with { Host = Req.Host with { InstalledHooks = ["block-destructive"] } });

        Assert.Contains("rtk-rewrite", plan.Hooks.Kept, StringComparer.Ordinal);
        Assert.True(Has(plan, InnerRuleEffect.Deny, "Bash", p => p == "rtk git push:*"));
        Assert.True(Has(plan, InnerRuleEffect.Deny, "Bash", p => p == "rtk gh:*"));
        Assert.True(Has(plan, InnerRuleEffect.Deny, "Bash", p => p == "rtk env"));
        Assert.False(Has(withoutRtk, InnerRuleEffect.Deny, "Bash", p => p.StartsWith("rtk ", StringComparison.Ordinal)));
    }

    [Fact, Trait("Lane", "unit")]
    public void Write_scope_may_edit_the_worktree_and_the_run_directory_and_read_only_only_the_run_directory()
    {
        var write = SandboxPlannerTests.Build(Req.WriteScope);
        var read = SandboxPlannerTests.Build(Req.ReadOnly);

        Assert.True(Has(write, InnerRuleEffect.Allow, "Edit", p => p == Req.Worktree + "/**"));
        Assert.True(Has(write, InnerRuleEffect.Allow, "Edit", p => p == Req.RunDirectory + "/**"));
        Assert.True(Has(write, InnerRuleEffect.Allow, "mcp__sindri-step__*", _ => true));
        Assert.True(Has(read, InnerRuleEffect.Allow, "Edit", p => p == Req.RunDirectory + "/**"));
        Assert.False(Has(read, InnerRuleEffect.Allow, "Edit", p => p.StartsWith(Req.Worktree, StringComparison.Ordinal)));
        Assert.True(Has(read, InnerRuleEffect.Allow, "Bash", p => p == "git status:*"));
        Assert.False(Has(read, InnerRuleEffect.Allow, "Bash", p => p.Length == 0));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_non_empty_allowed_tools_list_replaces_the_tool_entries_and_the_denies_stay()
    {
        var request = Req.WriteScope with { Policy = new StepPolicy(["Read", "Grep"], StepPermissionMode.Default, Req.Worktree) };

        var plan = SandboxPlannerTests.Build(request);

        Assert.True(Has(plan, InnerRuleEffect.Allow, "Read", p => p.Length == 0));
        Assert.True(Has(plan, InnerRuleEffect.Allow, "Grep", p => p.Length == 0));
        Assert.False(Has(plan, InnerRuleEffect.Allow, "Bash", _ => true));
        Assert.True(Has(plan, InnerRuleEffect.Deny, "Bash", p => p == "git push:*"));
    }

    [Fact, Trait("Lane", "unit")]
    public void Hooks_in_the_plan_are_the_session_inventory()
    {
        var expected = HookPermissionPolicy.ForSession();

        var hooks = SandboxPlannerTests.Build(Req.WriteScope).Hooks;

        Assert.Equal(expected.Kept, hooks.Kept);
        Assert.Equal(expected.Omitted, hooks.Omitted);
    }

    [Fact, Trait("Lane", "unit")]
    public void Attended_inner_mode_asks_with_the_same_deny_rules()
    {
        var step = SandboxPlannerTests.Build(Req.WriteScope);
        var attended = SandboxPlannerTests.Build(Req.Attended);

        Assert.Equal(InnerMode.Ask, attended.Policy.Mode);
        Assert.Equal(
            [.. step.Policy.Rules.Where(r => r.Effect == InnerRuleEffect.Deny)],
            [.. attended.Policy.Rules.Where(r => r.Effect == InnerRuleEffect.Deny)]);
    }

    [Fact, Trait("Lane", "unit")]
    public void Attended_session_writes_the_worktree_has_no_wall_and_the_summary_says_attended()
    {
        var plan = SandboxPlannerTests.Build(Req.Attended);

        Assert.Equal(PlanMountKind.ReadWrite, plan.Mounts.Last(m => m.Path == Req.Worktree).Kind);
        Assert.Null(plan.Limits.Wall);
        Assert.Null(plan.Limits.RuntimeMax);
        Assert.True(plan.Attended);
        Assert.True(plan.ToSummary().Attended);
        Assert.Null(plan.ToSummary().Limits.Wall);
    }

    [Fact, Trait("Lane", "unit")]
    public void Attended_session_never_gets_agent_app_or_the_github_group()
    {
        var settings = new SandboxSettings(Credentials: new SandboxCredentials(Github: GithubCredentials.AgentApp));

        var step = SandboxPlannerTests.Build(Req.WriteScope with { Settings = settings });
        var attended = SandboxPlannerTests.Build(Req.Attended with { Settings = settings });

        Assert.Equal(GithubCredentials.AgentApp, step.Github);
        Assert.Contains("github", step.Network.Groups.Select(g => g.Name), StringComparer.Ordinal);
        Assert.Equal(GithubCredentials.None, attended.Github);
        Assert.DoesNotContain("github", attended.Network.Groups.Select(g => g.Name), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_profile_settings_reach_the_plan()
    {
        var settings = new SandboxSettings(
            Limits: new SandboxLimits(Cpu: "25%", Sessions: 2),
            Network: new SandboxNetwork(Mode: NetworkMode.Learn),
            Inner: InnerSandbox.Off);

        var plan = SandboxPlannerTests.Build(Req.WriteScope with { Settings = settings });

        Assert.Equal(200, plan.Limits.CpuQuotaPercent);
        Assert.Equal(2, plan.Limits.Sessions);
        Assert.Equal(NetworkMode.Learn, plan.Network.Mode);
        Assert.Equal(InnerSandbox.Off, plan.Inner);
    }

    [Fact, Trait("Lane", "unit")]
    public void Repo_declaration_overrides_the_profile_per_key()
    {
        var settings = new SandboxSettings(Repos: new Dictionary<string, SandboxRepoSettings>(StringComparer.Ordinal) { ["web"] = new(Hosts: ["pypi.org"]), ["other"] = new(Hosts: ["nope.example"]) });

        var plan = SandboxPlannerTests.Build(Req.WriteScope with { Settings = settings });

        Assert.Contains("pypi.org", plan.Network.Groups.SelectMany(g => g.Hosts), StringComparer.Ordinal);
        Assert.DoesNotContain("nope.example", plan.Network.Groups.SelectMany(g => g.Hosts), StringComparer.Ordinal);
        Assert.Null(RepoDeclaration.For(settings, RepoName.From("none")).Hosts);
        Assert.Equal(["pypi.org"], RepoDeclaration.For(settings, RepoName.From("web")).Hosts);
    }
}
