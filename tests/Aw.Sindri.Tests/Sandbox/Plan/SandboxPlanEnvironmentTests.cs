using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Credentials;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Core.Workflow.Engine;
using Req = Aw.Sindri.Testing.Sandbox.SandboxRequests;

namespace Aw.Sindri.Tests.Sandbox.Plan;

/// <summary>The environment and git settings of a plan (sandbox spec §3.3 rules 3-4, §3.4).</summary>
public sealed class SandboxPlanEnvironmentTests
{
    private static Dictionary<string, string?> Env(SandboxPlan plan) => plan.Environment.ToDictionary(e => e.Name, e => e.Value, StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void Environment_has_no_secret_name_except_the_list()
    {
        var plan = SandboxPlannerTests.Build(Req.WriteScope);   // the host environment carries GH_TOKEN and AWS_SECRET_ACCESS_KEY

        var names = plan.Environment.Select(e => e.Name).ToList();

        Assert.DoesNotContain("GH_TOKEN", names, StringComparer.Ordinal);
        Assert.DoesNotContain("AWS_SECRET_ACCESS_KEY", names, StringComparer.Ordinal);
        Assert.Empty(EnvironmentAllowlist.Violations(names));
        Assert.All(plan.Environment, e => Assert.True(EnvironmentAllowlist.IsListed(e.Name), e.Name));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_secret_name_in_the_login_environment_fails_the_env_canary()
    {
        var login = Req.OwnerCopyLogin() with { Environment = new Dictionary<string, string>(StringComparer.Ordinal) { ["CLAUDE_CONFIG_DIR"] = "/x", ["GH_TOKEN"] = "ghp_planted" } };

        var result = SandboxPlanner.Build(SandboxMechanism.PassThrough, Req.WriteScope with { Login = login });

        Assert.Equal(SindriErrors.LaunchSelfCheckFailed.Code, result.Error.Code);
        Assert.Equal("env", SandboxRefusal.ReasonOf(result.Error));
        Assert.DoesNotContain("ghp_planted", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_model_api_key_name_is_allowed_from_the_login_only()
    {
        var login = Req.OwnerCopyLogin() with { Environment = new Dictionary<string, string>(StringComparer.Ordinal) { ["ANTHROPIC_API_KEY"] = "sk-x" } };

        var plan = SandboxPlannerTests.Build(Req.WriteScope with { Login = login });

        Assert.Contains("ANTHROPIC_API_KEY", plan.Environment.Select(e => e.Name), StringComparer.Ordinal);
        Assert.DoesNotContain("ANTHROPIC_API_KEY", SandboxPlannerTests.Build(Req.WriteScope).Environment.Select(e => e.Name), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Heavy_lock_held_is_never_in_the_environment()
    {
        var held = SandboxPlannerTests.Build(Req.WriteScope with { SlotHeld = true });
        var free = SandboxPlannerTests.Build(Req.WriteScope);

        Assert.DoesNotContain("AW_HEAVY_LOCK_HELD", held.Environment.Select(e => e.Name), StringComparer.Ordinal);
        Assert.DoesNotContain("AW_HEAVY_LOCK_HELD", free.Environment.Select(e => e.Name), StringComparer.Ordinal);
        Assert.Equal("1", Env(held)["AW_SANDBOX_SLOT_HELD"]);
        Assert.DoesNotContain("AW_SANDBOX_SLOT_HELD", free.Environment.Select(e => e.Name), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_values_the_sandbox_sets_always_are_there()
    {
        var env = Env(SandboxPlannerTests.Build(Req.WriteScope));

        Assert.Equal(Req.Home, env["HOME"]);
        Assert.Equal("1", env["AW_SANDBOX"]);
        Assert.Equal("true", env["npm_config_ignore_scripts"]);
        Assert.Equal("4", env["DOTNET_PROCESSOR_COUNT"]);
        Assert.Equal("1", env["MSBUILDDISABLENODEREUSE"]);
        Assert.Equal("0", env["DOTNET_CLI_USE_MSBUILD_SERVER"]);
        Assert.Equal("/tmp", env["TMPDIR"]);
        Assert.Equal("/tmp/dotnet-home", env["DOTNET_CLI_HOME"]);
        Assert.Equal(Req.Home + "/.claude-run", env["CLAUDE_CONFIG_DIR"]);
        Assert.Equal(Req.Worktree, env["AW_SINDRI_WORKTREE"]);
        Assert.Equal("sindri/x/1/work", env["AW_SINDRI_BRANCH"]);
        Assert.Equal(Req.RunDirectory, env["AW_SINDRI_RUN_DIR"]);
        Assert.True(env.ContainsKey("AW_SINDRI_LAUNCH_SECRET"));
        Assert.Null(env["AW_SINDRI_LAUNCH_SECRET"]);   // session wrap supplies it
        Assert.StartsWith(Req.RunArea + "/shims:", env["PATH"], StringComparison.Ordinal);
        Assert.Equal("http://127.0.0.1:41234", env["HTTPS_PROXY"]);
        Assert.Equal("http://127.0.0.1:41234", env["https_proxy"]);
        Assert.Equal("127.0.0.1,localhost,::1", env["NO_PROXY"]);
        Assert.DoesNotContain("AW_SINDRI_HOME", env.Keys, StringComparer.Ordinal);   // it is the default
    }

    [Fact, Trait("Lane", "unit")]
    public void Sindri_home_is_set_only_when_it_is_not_the_default_and_heavy_cpus_feeds_the_processor_count()
    {
        var host = Req.Host with
        {
            SindriHome = "/custom/home",
            Environment = new Dictionary<string, string>(Req.Host.Environment, StringComparer.Ordinal) { ["AW_HEAVY_CPUS"] = "6" },
        };

        var paths = Req.WriteScope.Paths with { RunDirectory = "/custom/home/runs/1", RunArea = "/custom/home/areas/1" };

        var env = Env(SandboxPlannerTests.Build(Req.WriteScope with { Host = host, Paths = paths }));

        Assert.Equal("/custom/home", env["AW_SINDRI_HOME"]);
        Assert.Equal("6", env["DOTNET_PROCESSOR_COUNT"]);
    }

    [Fact, Trait("Lane", "unit")]
    public void Git_settings_are_in_the_plan_and_travel_as_command_line_environment()
    {
        var plan = SandboxPlannerTests.Build(Req.WriteScope);
        var env = Env(plan);

        Assert.Equal(plan.Git.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), env["GIT_CONFIG_COUNT"]);
        Assert.Equal(Req.RunArea + "/gitconfig.empty", env["GIT_CONFIG_GLOBAL"]);
        Assert.Contains(plan.Git, g => string.Equals(g.Key, "core.hooksPath", StringComparison.Ordinal) && g.Value == Req.RunArea + "/githooks");
        Assert.Contains(plan.Git, g => string.Equals(g.Key, "safe.directory", StringComparison.Ordinal) && g.Value == Req.Worktree);
    }

    [Fact, Trait("Lane", "unit")]
    public void Source_is_in_the_plan_hash_and_survives_resume()
    {
        var owner = SandboxPlannerTests.Build(Req.WriteScope);
        var separateSettings = new SandboxSettings(Login: new SandboxLogin(Source: ModelLoginSource.SeparateLogin));
        var separateLogin = Req.OwnerCopyLogin() with { Source = ModelLoginSource.SeparateLogin };

        var separate = SandboxPlannerTests.Build(Req.WriteScope with { Settings = separateSettings, Login = separateLogin });

        Assert.Equal(ModelLoginSource.SeparateLogin, separate.Login);
        Assert.NotEqual(owner.PlanHash, separate.PlanHash, StringComparer.Ordinal);
        Assert.Equal(ModelLoginSource.SeparateLogin, SandboxPlanner.FromStored(separate).Value.Login);
    }

    [Fact, Trait("Lane", "unit")]
    public void Plan_hash_is_stable_across_launches_of_the_same_policy()
    {
        var first = SandboxPlannerTests.Build(Req.WriteScope);
        var other = Req.WriteScope with
        {
            Launch = LaunchId.From("LCH-" + new string('e', 32)),
            Paths = new SandboxPaths(Req.Repo, Req.Repo + "/.git", "/work/web/.claude/worktrees/sindri-x-2", Req.SindriHome + "/workflow/runs/x/s/2", Req.SindriHome + "/workflow/areas/run-2"),
            RunNamespace = "sindri/x/2",
            Policy = new StepPolicy([], StepPermissionMode.Default, "/work/web/.claude/worktrees/sindri-x-2"),
            Net = new SandboxNetFacts("/run/user/1000/sindri/ffff0000", 7480, 50000),
            Login = Req.OwnerCopyLogin() with
            {
                Mounts = [new LoginMount(Req.Home + "/.claude", Req.Home + "/.claude", MountMode.ReadOnly), new LoginMount(Req.SindriHome + "/workflow/areas/run-2/login", Req.Home + "/.claude-run", MountMode.ReadWrite)],
            },
        };

        var second = SandboxPlannerTests.Build(other);

        Assert.Matches("^[0-9a-f]{12}$", first.PlanHash);
        Assert.Equal(first.PlanHash, second.PlanHash);
    }

    [Fact, Trait("Lane", "unit")]
    public void Plan_hash_changes_with_a_mount_a_policy_or_a_group()
    {
        var baseline = SandboxPlannerTests.Build(Req.WriteScope).PlanHash;

        Assert.NotEqual(baseline, SandboxPlannerTests.Build(Req.WriteScope with { ExtraDeny = ["/holdout"] }).PlanHash, StringComparer.Ordinal);
        Assert.NotEqual(baseline, SandboxPlannerTests.Build(Req.ReadOnly).PlanHash, StringComparer.Ordinal);
        Assert.NotEqual(baseline, SandboxPlannerTests.Build(Req.WriteScope with { OmitGroups = ["packages"] }).PlanHash, StringComparer.Ordinal);
        Assert.NotEqual(baseline, SandboxPlannerTests.Build(Req.Attended).PlanHash, StringComparer.Ordinal);
    }
}
