using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Req = Aw.Sindri.Testing.Sandbox.SandboxRequests;

namespace Aw.Sindri.Tests.Sandbox.Plan;

/// <summary>Bash and write patterns, model-key placeholders, the write set on home entries, the git directory and the run namespace.</summary>
public sealed class SandboxPlanPatternAndPathTests
{
    private static readonly string[] _writeTools = ["Write", "Edit", "NotebookEdit"];

    private static List<string> Bash(SandboxPlan plan) =>
        [.. plan.Policy.Rules.Where(r => r.Effect == InnerRuleEffect.Allow && string.Equals(r.Tool, "Bash", StringComparison.Ordinal)).Select(r => r.Pattern)];

    private static SandboxPlan ReadOnlyWith(params string[] tools) =>
        SandboxPlannerTests.Build(Req.ReadOnly with { Policy = new StepPolicy(tools, StepPermissionMode.ReadOnly, WritablePath: null) });

    private static void RefusedAsLogin(SandboxRequest request)
    {
        var result = SandboxPlanner.Build(SandboxMechanism.PassThrough, request);

        Assert.True(result.IsError);
        Assert.Equal(SindriErrors.LaunchSelfCheckFailed.Code, result.Error.Code);
        Assert.Equal("login", SandboxRefusal.ReasonOf(result.Error));
    }

    [Fact, Trait("Lane", "unit")]
    public void Read_only_step_keeps_a_narrower_bash_pattern() =>
        Assert.Equal(["git status:*"], Bash(ReadOnlyWith("Bash(git status:*)")));

    [Fact, Trait("Lane", "unit")]
    public void Read_only_step_keeps_a_narrower_still_pattern_and_drops_what_is_not_read_only()
    {
        Assert.Equal(["git log --oneline:*"], Bash(ReadOnlyWith("Bash(git log --oneline:*)", "Bash(rm:*)", "Bash(git push:*)")));
        Assert.Empty(Bash(ReadOnlyWith("Bash(rm:*)")));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("Bash(git status)")]
    [InlineData("Bash(git status *)")]
    [InlineData("Bash(git log --oneline *)")]
    public void Read_only_step_keeps_the_exact_and_the_star_forms_of_a_read_only_command(string tool) =>
        Assert.Equal([tool[5..^1]], Bash(ReadOnlyWith(tool)));

    [Theory, Trait("Lane", "unit")]
    [InlineData("Bash(git push)")]
    [InlineData("Bash(git statusx *)")]
    [InlineData("Bash(rm *)")]
    public void Read_only_step_drops_the_exact_and_the_star_forms_of_anything_else(string tool) =>
        Assert.Empty(Bash(ReadOnlyWith(tool)));

    [Fact, Trait("Lane", "unit")]
    public void Read_only_step_maps_a_bare_bash_to_the_whole_read_only_set() =>
        Assert.Equal(["git status:*", "git diff:*", "git log:*", "git show:*", "sindri:*"], Bash(ReadOnlyWith("Bash")));

    [Theory, Trait("Lane", "unit")]
    [InlineData("ANTHROPIC_API_KEY")]
    [InlineData("OPENAI_API_KEY")]
    public void Model_key_values_never_enter_the_plan_or_its_hash(string name)
    {
        SandboxRequest With(string key) => Req.WriteScope with
        {
            Login = Req.OwnerCopyLogin() with { Environment = new Dictionary<string, string>(StringComparer.Ordinal) { [name] = key } },
        };

        var first = SandboxPlannerTests.Build(With("sk-test-one"));
        var second = SandboxPlannerTests.Build(With("sk-test-two"));

        var serialized = System.Text.Json.JsonSerializer.Serialize(first);
        Assert.Null(first.Environment.Single(e => e.Name == name).Value);
        Assert.Equal(first.PlanHash, second.PlanHash);
        Assert.DoesNotContain("sk-test", serialized, StringComparison.Ordinal);
        Assert.Contains(name, serialized, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_attended_request_with_a_step_policy_is_an_error() =>
        _ = Assert.Throws<ArgumentException>(() => SandboxPlanner.Build(SandboxMechanism.PassThrough, Req.Attended with { Policy = new StepPolicy([], StepPermissionMode.Default, Req.Worktree) }));

    [Fact, Trait("Lane", "unit")]
    public void Agent_home_write_set_applies_to_home_allow_and_cache_paths()
    {
        static SandboxRequest Allow(string path, MountMode mode) => Req.WriteScope with { Settings = new SandboxSettings(Fs: new SandboxFs(HomeAllow: [new HomeAllowEntry(path, mode)])) };

        RefusedAsLogin(Allow(".claude", MountMode.ReadWrite));
        RefusedAsLogin(Allow(".codex/hooks", MountMode.ReadWrite));
        RefusedAsLogin(Req.WriteScope with { Settings = new SandboxSettings(Repos: new Dictionary<string, SandboxRepoSettings>(StringComparer.Ordinal) { ["web"] = new(CachePaths: [".codex/hooks"]) }) });
        RefusedAsLogin(Req.WriteScope with { Settings = new SandboxSettings(Repos: new Dictionary<string, SandboxRepoSettings>(StringComparer.Ordinal) { ["web"] = new(HomeAllow: [new HomeAllowEntry(".claude/settings.json", MountMode.ReadWrite)]) }) });
        _ = SandboxPlannerTests.Build(Allow(".claude/projects", MountMode.ReadWrite));
        _ = SandboxPlannerTests.Build(Allow(".claude", MountMode.ReadOnly));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_spike_turns_the_auto_updater_off()
    {
        var plan = SandboxPlannerTests.Build(Req.WriteScope);

        Assert.Equal("1", plan.Environment.Single(e => e.Name == "DISABLE_AUTOUPDATER").Value);
        Assert.Contains("DISABLE_AUTOUPDATER", EnvironmentAllowlist.Names.Keys, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Edit_and_write_patterns_are_normalized_and_a_dotdot_pattern_is_dropped()
    {
        var policy = new StepPolicy([$"Write({Req.Worktree}/../../etc/**)", $"Edit({Req.Worktree}/a/../b/**)", $"Write({Req.Worktree}/ok/**)", "NotebookEdit"], StepPermissionMode.Default, Req.Worktree);

        var plan = SandboxPlannerTests.Build(Req.WriteScope with { Policy = policy });

        var writes = plan.Policy.Rules.Where(r => r.Effect == InnerRuleEffect.Allow && _writeTools.Contains(r.Tool, StringComparer.Ordinal)).Select(r => $"{r.Tool} {r.Pattern}").ToList();
        Assert.DoesNotContain(writes, w => w.Contains("..", StringComparison.Ordinal));
        Assert.Contains($"Write {Req.Worktree}/ok/**", writes, StringComparer.Ordinal);
        Assert.Contains($"NotebookEdit {Req.Worktree}/**", writes, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_read_only_plan_maps_notebook_edit_to_the_run_directory()
    {
        var plan = ReadOnlyWith("NotebookEdit");

        Assert.Contains(plan.Policy.Rules, r => r.Effect == InnerRuleEffect.Allow && r.Tool == "NotebookEdit" && r.Pattern == Req.RunDirectory + "/**");
        Assert.DoesNotContain(plan.Policy.Rules, r => r.Effect == InnerRuleEffect.Allow && r.Tool == "NotebookEdit" && r.Pattern.Length == 0);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("/home")]
    [InlineData("/home/owner")]
    [InlineData("/elsewhere/.git")]
    public void A_git_directory_outside_the_repo_is_refused(string git)
    {
        var result = SandboxPlanner.Build(SandboxMechanism.PassThrough, Req.WriteScope with { Paths = Req.WriteScope.Paths with { GitCommonDir = git } });

        Assert.Equal(SindriErrors.LaunchUnconfinable.Code, result.Error.Code);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("/work/web/src")]
    [InlineData("/work/web/.gitx")]
    [InlineData("/work/web/.claude/worktrees")]
    public void A_git_directory_inside_the_repo_but_not_dot_git_is_refused(string git)
    {
        var result = SandboxPlanner.Build(SandboxMechanism.PassThrough, Req.WriteScope with { Paths = Req.WriteScope.Paths with { GitCommonDir = git } });

        Assert.Equal(SindriErrors.LaunchUnconfinable.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_dot_git_directory_itself_is_accepted() =>
        _ = SandboxPlannerTests.Build(Req.WriteScope with { Paths = Req.WriteScope.Paths with { GitCommonDir = Req.Repo + "/.git" } });

    [Fact, Trait("Lane", "unit")]
    public void A_git_directory_inside_the_repo_is_accepted() =>
        _ = SandboxPlannerTests.Build(Req.WriteScope with { Paths = Req.WriteScope.Paths with { GitCommonDir = Req.Repo + "/.git/modules/m" } });

    [Fact, Trait("Lane", "unit")]
    public void The_hash_ignores_the_terminal_locale_user_and_shell()
    {
        var host = Req.Host with
        {
            Environment = new Dictionary<string, string>(Req.Host.Environment, StringComparer.Ordinal) { ["TERM"] = "dumb", ["LANG"] = "de_DE.UTF-8", ["USER"] = "other", ["SHELL"] = "/bin/zsh" },
        };

        Assert.Equal(SandboxPlannerTests.Build(Req.WriteScope).PlanHash, SandboxPlannerTests.Build(Req.WriteScope with { Host = host }).PlanHash);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("sindri/x/1/2")]
    [InlineData("sindri/a..b/1")]
    [InlineData("sindri/.x/1")]
    [InlineData("sindri/x./1")]
    [InlineData("sindri/x/-")]
    public void The_run_namespace_is_exactly_three_git_safe_segments(string ns)
    {
        var result = SandboxPlanner.Build(SandboxMechanism.PassThrough, Req.WriteScope with { RunNamespace = ns });

        if (string.Equals(ns, "sindri/x/-", StringComparison.Ordinal))
        {
            Assert.True(result.IsSuccess);
        }
        else
        {
            Assert.Equal(SindriErrors.LaunchUnconfinable.Code, result.Error.Code);
        }
    }
}
