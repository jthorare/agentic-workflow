using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Credentials;
using Aw.Sindri.Core.Sandbox.Plan;
using Req = Aw.Sindri.Testing.Sandbox.SandboxRequests;

namespace Aw.Sindri.Tests.Sandbox.Plan;

/// <summary>#355 run 1, I3, I4 and I5: what a credential source may add to the plan, and the masks that follow what is actually bound.</summary>
public sealed class SandboxPlanLoginTests
{
    private static SandboxRequest WithEnv(params (string Name, string Value)[] env) =>
        Req.WriteScope with { Login = Req.OwnerCopyLogin() with { Environment = env.ToDictionary(e => e.Name, e => e.Value, StringComparer.Ordinal) } };

    private static SandboxRequest WithMounts(params LoginMount[] mounts) => Req.WriteScope with { Login = Req.OwnerCopyLogin() with { Mounts = mounts } };

    private static void RefusedAs(SandboxRequest request, string reason)
    {
        var result = SandboxPlanner.Build(SandboxMechanism.PassThrough, request);

        Assert.True(result.IsError);
        Assert.Equal(SindriErrors.LaunchSelfCheckFailed.Code, result.Error.Code);
        Assert.Equal(reason, SandboxRefusal.ReasonOf(result.Error));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("HTTPS_PROXY")]
    [InlineData("NO_PROXY")]
    [InlineData("PATH")]
    [InlineData("HOME")]
    [InlineData("GIT_CONFIG_GLOBAL")]
    [InlineData("GIT_CONFIG_COUNT")]
    [InlineData("GIT_CONFIG_KEY_0")]
    [InlineData("AW_SANDBOX")]
    [InlineData("AW_SINDRI_LAUNCH_SECRET")]
    public void A_login_cannot_override_the_sandbox_variables(string name) => RefusedAs(WithEnv((name, "x")), "env");

    [Theory, Trait("Lane", "unit")]
    [InlineData("CLAUDE_CONFIG_DIR")]
    [InlineData("CODEX_HOME")]
    [InlineData("ANTHROPIC_API_KEY")]
    [InlineData("OPENAI_API_KEY")]
    public void A_login_may_set_the_agent_home_and_the_model_key(string name)
    {
        var plan = SandboxPlannerTests.Build(WithEnv((name, "/home/owner/x")));

        Assert.Equal(1, plan.Environment.Count(e => e.Name == name));
    }

    [Fact, Trait("Lane", "unit")]
    public void Environment_names_are_unique()
    {
        var names = SandboxPlannerTests.Build(Req.WriteScope).Environment.Select(e => e.Name).ToList();

        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact, Trait("Lane", "unit")]
    public void A_codex_home_under_tmp_is_refused_because_codex_will_not_make_its_helpers_there() =>
        RefusedAs(WithEnv(("CODEX_HOME", "/tmp/codex")), "login");

    [Fact, Trait("Lane", "unit")]
    public void The_credential_mask_follows_what_is_bound_so_a_home_allow_entry_for_the_agent_home_is_masked()
    {
        var settings = new SandboxSettings(Fs: new SandboxFs(HomeAllow: [new HomeAllowEntry(".claude", MountMode.ReadOnly), new HomeAllowEntry(".codex", MountMode.ReadOnly)]));

        var plan = SandboxPlannerTests.Build(Req.WriteScope with { Settings = settings, Login = null });

        Assert.Equal(PlanMountKind.Deny, plan.Mounts.Last(m => m.Path == Req.Home + "/.claude/.credentials.json").Kind);
        Assert.Equal(PlanMountKind.Deny, plan.Mounts.Last(m => m.Path == Req.Home + "/.codex/auth.json").Kind);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_separate_login_home_from_another_host_path_is_not_masked_because_its_login_is_the_sessions_own()
    {
        var settings = new SandboxSettings(Login: new SandboxLogin(Source: ModelLoginSource.SeparateLogin));
        var login = new PreparedLogin(
            ModelLoginSource.SeparateLogin,
            [new LoginMount("/home/owner/.agentic-workflow/sindri-app/agent-homes/claude-code", Req.Home + "/.claude", MountMode.ReadOnly)],
            new Dictionary<string, string>(StringComparer.Ordinal) { ["CLAUDE_CONFIG_DIR"] = Req.Home + "/.claude" },
            [Req.Home + "/.claude"],
            new LoginHandle("h"));

        var plan = SandboxPlannerTests.Build(Req.WriteScope with { Settings = settings, Login = login });

        Assert.DoesNotContain(plan.Mounts, m => m.Path == Req.Home + "/.claude/.credentials.json");
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("projects/web-key")]
    [InlineData("sessions")]
    [InlineData("session-env")]
    [InlineData("shell-snapshots")]
    [InlineData("file-history")]
    [InlineData("todos")]
    [InlineData("history.jsonl")]
    [InlineData("backups")]
    [InlineData("cache")]
    [InlineData("state")]
    public void Agent_home_write_set_is_exactly_the_named_one_for_claude(string entry)
    {
        var accepted = WithMounts(new LoginMount(Req.Home + "/.claude", Req.Home + "/.claude", MountMode.ReadOnly), new LoginMount($"{Req.RunArea}/w/{entry}", $"{Req.Home}/.claude/{entry}", MountMode.ReadWrite));

        _ = SandboxPlannerTests.Build(accepted);
        Assert.True(AgentHomeWriteSet.Allows(Req.Home + "/.claude", entry));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("hooks")]
    [InlineData("settings.json")]
    [InlineData("skills")]
    [InlineData("rules")]
    [InlineData("keybindings.json")]
    [InlineData("projectsx")]
    public void Agent_home_write_set_refuses_everything_else_for_claude(string entry)
    {
        var refused = WithMounts(new LoginMount(Req.Home + "/.claude", Req.Home + "/.claude", MountMode.ReadOnly), new LoginMount($"{Req.RunArea}/w/{entry}", $"{Req.Home}/.claude/{entry}", MountMode.ReadWrite));

        RefusedAs(refused, "login");
        Assert.False(AgentHomeWriteSet.Allows(Req.Home + "/.claude", entry));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("sessions", true)]
    [InlineData("log", true)]
    [InlineData("memories", true)]
    [InlineData("shell_snapshots", true)]
    [InlineData("cache", true)]
    [InlineData("tmp", true)]
    [InlineData("config.toml", false)]
    [InlineData("rules", false)]
    [InlineData("auth.json", false)]
    public void Agent_home_write_set_is_exactly_the_named_one_for_codex(string entry, bool allowed)
    {
        Assert.Equal(allowed, AgentHomeWriteSet.Allows(Req.Home + "/.codex", entry));
        var request = WithMounts(new LoginMount(Req.Home + "/.codex", Req.Home + "/.codex", MountMode.ReadOnly), new LoginMount($"{Req.RunArea}/w/{entry}", $"{Req.Home}/.codex/{entry}", MountMode.ReadWrite));

        if (allowed)
        {
            _ = SandboxPlannerTests.Build(request);
        }
        else
        {
            RefusedAs(request, "login");
        }
    }
}
