using Aw.Sindri.Core;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Core.Workflow.Engine;
using Req = Aw.Sindri.Testing.Sandbox.SandboxRequests;

namespace Aw.Sindri.Tests.Sandbox.Plan;

/// <summary>Sandbox spec revision 7 (§3.1, §3.4): the launch's session home, the real folders its links resolve to, and the C# binaries.</summary>
public sealed class SandboxPlanSessionHomeTests
{
    private static string Session(string home = Req.SindriHome) => $"{home}-sessions/{Req.WriteScope.Launch.Value}";

    private static int At(SandboxPlan plan, string path, PlanMountKind kind) =>
        plan.Mounts.ToList().FindLastIndex(m => string.Equals(m.Path, path, StringComparison.Ordinal) && m.Kind == kind);

    private static SandboxRequest Custom()
    {
        const string home = "/srv/sindri";
        var paths = Req.WriteScope.Paths with { RunDirectory = home + "/workflow/runs/x/s/1", RunArea = home + "/workflow/areas/run-1" };
        return Req.WriteScope with { Host = Req.Host with { SindriHome = home }, Paths = paths };
    }

    private static void AssertLinksResolve(SandboxPlan plan, string home)
    {
        var session = Session(home);
        var denyReal = At(plan, home, PlanMountKind.Deny);
        Assert.True(denyReal >= 0 || At(plan, Req.StateDir, PlanMountKind.Deny) >= 0);
        Assert.True(At(plan, home + "-sessions", PlanMountKind.Deny) >= 0, "other launches' session homes are denied");

        // the masks make the mountpoints exist, then the binds go shallow to deep
        string[] order = [home, home + "-sessions", home + "/launches", home + "/approved", session, session + "/control.json", session + "/gh-config"];
        var maskReal = At(plan, home, PlanMountKind.Tmpfs);
        var maskSessions = At(plan, home + "-sessions", PlanMountKind.Tmpfs);
        Assert.True(maskReal > denyReal && maskSessions > denyReal, "the masks follow the denies");
        Assert.True(maskReal >= 0 && maskSessions >= 0);
        var launches = At(plan, home + "/launches", PlanMountKind.ReadWrite);
        var approved = At(plan, home + "/approved", PlanMountKind.ReadOnly);
        var sessionHome = At(plan, session, PlanMountKind.ReadOnly);
        var control = At(plan, session + "/control.json", PlanMountKind.ReadOnly);
        var ghConfig = At(plan, session + "/gh-config", PlanMountKind.ReadWrite);
        Assert.All((int[])[launches, approved, sessionHome, control, ghConfig], i => Assert.True(i > Math.Max(maskReal, maskSessions)));
        Assert.True(sessionHome < control && sessionHome < ghConfig, "the session home is bound before what is inside it");
        Assert.All(order.Skip(2), p => Assert.Null(plan.Mounts.Last(m => string.Equals(m.Path, p, StringComparison.Ordinal)).Source));   // absolute, at their real paths
        Assert.DoesNotContain(plan.Mounts, m => m.Path.StartsWith(home + "/auth", StringComparison.Ordinal) && m.Kind != PlanMountKind.Deny);
    }

    [Fact, Trait("Lane", "unit")]
    public void Session_home_links_resolve_in_plan() => AssertLinksResolve(SandboxPlannerTests.Build(Req.WriteScope), Req.SindriHome);

    [Fact, Trait("Lane", "unit")]
    public void Session_home_links_resolve_for_a_custom_home_outside_the_state_directory() => AssertLinksResolve(SandboxPlannerTests.Build(Custom()), "/srv/sindri");

    [Fact, Trait("Lane", "unit")]
    public void Nothing_binds_the_real_home_but_its_launches_and_approved_folders_and_the_run_areas()
    {
        var plan = SandboxPlannerTests.Build(Req.WriteScope);

        var bound = plan.Mounts.Where(m => m.Kind is PlanMountKind.ReadOnly or PlanMountKind.ReadWrite or PlanMountKind.PerLaunchDir && m.Path.StartsWith(Req.SindriHome + "/", StringComparison.Ordinal)).Select(m => m.Path);

        Assert.All(bound, p => Assert.True(
            p.StartsWith(Req.SindriHome + "/launches", StringComparison.Ordinal) || p.StartsWith(Req.SindriHome + "/approved", StringComparison.Ordinal) || p.StartsWith(Req.SindriHome + "/workflow/", StringComparison.Ordinal), p));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_session_home_is_not_in_the_plan_hash()
    {
        var a = SandboxPlannerTests.Build(Req.WriteScope);
        var b = SandboxPlannerTests.Build(Req.WriteScope with { Launch = LaunchId.From("LCH-" + new string('c', 32)) });

        Assert.Equal(a.PlanHash, b.PlanHash);
    }

    [Fact, Trait("Lane", "unit")]
    public void Wrapper_resolves_current_to_stamp()
    {
        var plan = SandboxPlannerTests.Build(Req.WriteScope);

        var stamp = plan.Mounts.Last(m => string.Equals(m.Path, Req.CsBinDir, StringComparison.Ordinal));
        var current = plan.Mounts.Last(m => string.Equals(m.Path, Req.StateDir + "/bin/cs/current", StringComparison.Ordinal));
        Assert.Equal(PlanMountKind.ReadOnly, stamp.Kind);
        Assert.Equal(PlanMountKind.ReadOnly, current.Kind);
        Assert.Equal(Req.CsBinDir, current.Source);   // the wrapper execs into bin/cs/current, a link that is absent in the sandbox
        Assert.True(At(plan, Req.CsBinDir, PlanMountKind.ReadOnly) > At(plan, Req.StateDir, PlanMountKind.Deny));
        Assert.Equal(PlanMountKind.ReadOnly, plan.Mounts.Last(m => string.Equals(m.Path, Req.Home + "/.local/bin", StringComparison.Ordinal)).Kind);   // ~/.local/bin/sindri
        Assert.DoesNotContain(plan.Mounts, m => string.Equals(m.Path, Req.StateDir + "/bin", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void No_installed_binaries_means_no_bin_cs_bind()
    {
        var plan = SandboxPlannerTests.Build(Req.WriteScope with { Host = Req.Host with { CsBinDir = string.Empty } });

        Assert.DoesNotContain(plan.Mounts, m => m.Path.StartsWith(Req.StateDir + "/bin/cs", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void DOTNET_ROOT_outside_the_home_is_bound_read_only()
    {
        var env = new Dictionary<string, string>(Req.Host.Environment, StringComparer.Ordinal) { ["DOTNET_ROOT"] = "/opt/dotnet" };

        var plan = SandboxPlannerTests.Build(Req.WriteScope with { Host = Req.Host with { Environment = env } });

        Assert.Equal(PlanMountKind.ReadOnly, plan.Mounts.Last(m => string.Equals(m.Path, "/opt/dotnet", StringComparison.Ordinal)).Kind);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("/home/owner/.agentic-workflow/bin/cs/")]
    [InlineData("/home/owner/.agentic-workflow/bin/cs/../../x")]
    [InlineData("/home/owner/.agentic-workflow/bin/other/stamp")]
    [InlineData("bin/cs/stamp")]
    public void A_stamp_outside_bin_cs_is_refused(string stamp)
    {
        var result = SandboxPlanner.Build(SandboxMechanism.PassThrough, Req.WriteScope with { Host = Req.Host with { CsBinDir = stamp } });

        Assert.True(result.IsError);
        Assert.Equal(SindriErrors.LaunchUnconfinable.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_agent_env_names_the_session_home_and_its_empty_gh_config()
    {
        var env = SandboxPlannerTests.Build(Req.WriteScope).Environment.ToDictionary(e => e.Name, e => e.Value, StringComparer.Ordinal);

        Assert.Equal(Session(), env["AW_SINDRI_HOME"]);
        Assert.Equal(Session() + "/gh-config", env["GH_CONFIG_DIR"]);
        Assert.DoesNotContain("AW_SINDRI_LAUNCH_SECRET_FILE", env.Keys, StringComparer.Ordinal);
        Assert.DoesNotContain("NVM_DIR", env.Keys, StringComparer.Ordinal);
        Assert.DoesNotContain("GH_TOKEN", env.Keys, StringComparer.Ordinal);
    }
}
