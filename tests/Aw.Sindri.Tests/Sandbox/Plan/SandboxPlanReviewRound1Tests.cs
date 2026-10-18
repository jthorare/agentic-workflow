using System.Reflection;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Credentials;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Features.OpenItem;
using Req = Aw.Sindri.Testing.Sandbox.SandboxRequests;

namespace Aw.Sindri.Tests.Sandbox.Plan;

/// <summary>#355 run 1: I1, I2, I6, I7, S1, S2, S4, S5 and N4.</summary>
public sealed class SandboxPlanReviewRound1Tests
{
    private static bool Has(SandboxPlan plan, InnerRuleEffect effect, string tool, Func<string, bool> pattern) =>
        plan.Policy.Rules.Any(r => r.Effect == effect && string.Equals(r.Tool, tool, StringComparison.Ordinal) && pattern(r.Pattern));

    [Fact, Trait("Lane", "unit")]
    public void Path_keeps_heavy_reachable_and_drops_the_rest_of_the_state_dir()
    {
        var host = Req.Host with
        {
            Environment = new Dictionary<string, string>(Req.Host.Environment, StringComparer.Ordinal) { ["PATH"] = "/usr/bin:/home/owner/.agentic-workflow/bin-r1b:/home/owner/.agentic-workflow/bin:/home/owner/.dotnet" },
        };

        var path = SandboxPlannerTests.Build(Req.WriteScope with { Host = host }).Environment.Single(e => e.Name == "PATH").Value!.Split(':');

        Assert.Equal(Req.RunArea + "/shims", path[0]);
        Assert.Equal(1, path.Count(p => p == Req.StateDir + "/bin-r1b"));
        Assert.DoesNotContain(Req.StateDir + "/bin", path, StringComparer.Ordinal);
        Assert.Contains("/home/owner/.dotnet", path, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Path_has_heavy_even_when_the_host_path_lacks_it()
    {
        var path = SandboxPlannerTests.Build(Req.WriteScope).Environment.Single(e => e.Name == "PATH").Value!.Split(':');

        Assert.Contains(Req.StateDir + "/bin-r1b", path, StringComparer.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(false, GithubCredentials.None, true)]
    [InlineData(false, GithubCredentials.AgentApp, true)]
    [InlineData(true, GithubCredentials.AgentApp, true)]
    [InlineData(false, GithubCredentials.None, false)]
    public void A_repo_declaration_cannot_add_github_or_packages_beyond_the_session_kind(bool attended, GithubCredentials github, bool writeScope)
    {
        var repo = new SandboxRepoSettings(Groups: ["github", "packages"]);

        var groups = HostGroups.Resolve(new GroupsInput(Settings: null, Repo: repo, Target: AgentId.From("claude-code"), WriteScope: writeScope, Attended: attended, Github: github, Omit: null)).Select(g => g.Name).ToList();

        Assert.Equal(github == GithubCredentials.AgentApp && !attended, groups.Contains("github"));
        Assert.Equal(writeScope, groups.Contains("packages"));
    }

    [Fact, Trait("Lane", "unit")]
    public void Inner_policy_names_the_default_mode_explicitly()
    {
        var step = SandboxPlannerTests.Build(Req.WriteScope).Policy;
        var attended = SandboxPlannerTests.Build(Req.Attended).Policy;

        Assert.Equal("dontAsk", step.DefaultMode);
        Assert.Equal("default", attended.DefaultMode);
        Assert.True(InnerPolicy.DisableBypassPermissions);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_attended_session_asks_for_bash_and_edits_and_allows_no_bash()
    {
        var plan = SandboxPlannerTests.Build(Req.Attended);

        Assert.True(Has(plan, InnerRuleEffect.Ask, "Bash", p => p.Length == 0));
        Assert.True(Has(plan, InnerRuleEffect.Ask, "Edit", p => p.Length == 0));
        Assert.True(Has(plan, InnerRuleEffect.Ask, "Write", p => p.Length == 0));
        Assert.False(Has(plan, InnerRuleEffect.Allow, "Bash", _ => true));
        Assert.DoesNotContain(SandboxPlannerTests.Build(Req.WriteScope).Policy.Rules, r => r.Effect == InnerRuleEffect.Ask);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_read_only_step_cannot_widen_bash_or_write_through_allowed_tools()
    {
        var policy = new StepPolicy(["Bash", "Bash(curl:*)", "Write(/etc/**)", "Edit", "Read"], StepPermissionMode.ReadOnly, WritablePath: null);

        var plan = SandboxPlannerTests.Build(Req.ReadOnly with { Policy = policy });

        Assert.False(Has(plan, InnerRuleEffect.Allow, "Bash", p => p.Length == 0));
        Assert.False(Has(plan, InnerRuleEffect.Allow, "Bash", p => p.StartsWith("curl", StringComparison.Ordinal)));
        Assert.True(Has(plan, InnerRuleEffect.Allow, "Bash", p => p == "git status:*"));
        Assert.False(Has(plan, InnerRuleEffect.Allow, "Write", p => p.StartsWith("/etc", StringComparison.Ordinal)));
        Assert.True(Has(plan, InnerRuleEffect.Allow, "Edit", p => p == Req.RunDirectory + "/**"));
        Assert.False(Has(plan, InnerRuleEffect.Allow, "Edit", p => p.StartsWith(Req.Worktree, StringComparison.Ordinal)));
        Assert.True(Has(plan, InnerRuleEffect.Allow, "Read", p => p.Length == 0));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_write_scope_step_keeps_an_explicit_write_pattern_only_inside_its_roots()
    {
        var policy = new StepPolicy([$"Write({Req.Worktree}/src/**)", "Write(/etc/**)"], StepPermissionMode.Default, Req.Worktree);

        var plan = SandboxPlannerTests.Build(Req.WriteScope with { Policy = policy });

        Assert.True(Has(plan, InnerRuleEffect.Allow, "Write", p => p == Req.Worktree + "/src/**"));
        Assert.False(Has(plan, InnerRuleEffect.Allow, "Write", p => p.StartsWith("/etc", StringComparison.Ordinal)));
    }

    [Fact, Trait("Lane", "unit")]
    public void Plan_hash_changes_with_an_environment_value()
    {
        var baseline = SandboxPlannerTests.Build(Req.WriteScope).PlanHash;
        var host = Req.Host with { Environment = new Dictionary<string, string>(Req.Host.Environment, StringComparer.Ordinal) { ["AW_HEAVY_CPUS"] = "6" } };

        Assert.NotEqual(baseline, SandboxPlannerTests.Build(Req.WriteScope with { Host = host }).PlanHash, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Node_options_comes_from_no_host_variable()
    {
        var host = Req.Host with { Environment = new Dictionary<string, string>(Req.Host.Environment, StringComparer.Ordinal) { ["NODE_OPTIONS"] = "--require /home/owner/x.js" } };

        Assert.DoesNotContain("NODE_OPTIONS", SandboxPlannerTests.Build(Req.WriteScope with { Host = host }).Environment.Select(e => e.Name), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Attended_open_before_the_switch_does_not_consult_the_sandbox_port()
    {
        // Removed by SB-5b, which switches 'sindri open' to the attended plan (sandbox spec 3.9).
        var asked = typeof(OpenItemRequest).Assembly.GetTypes()
            .Where(t => string.Equals(t.Namespace, typeof(OpenItemRequest).Namespace, StringComparison.Ordinal))
            .SelectMany(t => t.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            .SelectMany(c => c.GetParameters())
            .Where(p => p.ParameterType == typeof(ISandbox) || p.ParameterType == typeof(ModelCredentialSources))
            .ToList();

        Assert.Empty(asked);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("/home/owner/.agentic-workflow/../.ssh/id_rsa", "blocked")]
    [InlineData("/home/owner/.agentic-workflow/", "blocked")]
    [InlineData("/home/owner/./.agentic-workflow/x", "blocked")]
    public void Explain_normalizes_the_path_before_matching(string query, string expectedWord)
    {
        var plan = SandboxPlannerTests.Build(Req.WriteScope);

        var text = SandboxPlanRenderer.Explain(plan, query);

        if (query.Contains("..", StringComparison.Ordinal))
        {
            Assert.DoesNotContain("blocked: the ledger", text, StringComparison.Ordinal);
        }
        else
        {
            Assert.Contains(expectedWord, text, StringComparison.Ordinal);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void Explain_matches_a_command_against_a_rule_with_an_inner_wildcard()
    {
        var plan = SandboxPlannerTests.Build(Req.WriteScope);

        var text = SandboxPlanRenderer.Explain(plan, $"rm -rf {Req.StateDir}/locks");

        Assert.Contains("is blocked in the sandbox", text, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("build.sh")]
    [InlineData("git.exe")]
    [InlineData("notes.md")]
    public void A_file_name_is_not_taken_for_a_host(string query)
    {
        var text = SandboxPlanRenderer.Explain(SandboxPlannerTests.Build(Req.WriteScope), query);

        Assert.DoesNotContain("host group", text, StringComparison.Ordinal);
    }
}
