using Aw.Sindri.Core;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Core.Workflow.Engine;
using Req = Aw.Sindri.Testing.Sandbox.SandboxRequests;

namespace Aw.Sindri.Tests.Sandbox.Plan;

/// <summary>'sandbox exec' (sandbox spec §3.6): the stored plan turned into the reproduction sandbox. Pure.</summary>
public sealed class SandboxPlanExecTests
{
    private static readonly LaunchId _exec = LaunchId.From("LCH-" + new string('e', 32));
    private static readonly string[] _launcherNames = ["AW_SINDRI_LAUNCH_SECRET", "AW_SINDRI_HOME", "GH_CONFIG_DIR"];
    private const string ExecArea = "/home/owner/.agentic-workflow/sindri-app/workflow/areas/exec-eeeeeeeeeeee";
    private const string ExecNet = "/run/user/1000/aw-net/eeeeeeeeeeee";

    private static SandboxPlan Exec(SandboxPlan? stored = null) =>
        (stored ?? SandboxPlannerTests.Build(Req.WriteScope)).ForExec(_exec, ExecArea, ExecNet).Value;

    private static PlanMountKind KindAt(SandboxPlan plan, string path) => plan.Mounts.Last(m => string.Equals(m.Path, path, StringComparison.Ordinal)).Kind;

    [Fact, Trait("Lane", "unit")]
    public void The_worktree_the_run_directory_and_the_ref_directories_are_read_only()
    {
        var stored = SandboxPlannerTests.Build(Req.WriteScope);
        var git = Req.Repo + "/.git";
        var ns = Req.WriteScope.RunNamespace;
        Assert.Equal(PlanMountKind.ReadWrite, KindAt(stored, Req.Worktree));   // the stored plan wrote them

        var plan = Exec(stored);

        Assert.Equal(PlanMountKind.ReadOnly, KindAt(plan, Req.Worktree));
        Assert.Equal(PlanMountKind.ReadOnly, KindAt(plan, Req.RunDirectory));
        Assert.Equal(PlanMountKind.ReadOnly, KindAt(plan, $"{git}/refs/heads/{ns}"));
        Assert.Equal(PlanMountKind.ReadOnly, KindAt(plan, $"{git}/logs/refs/heads/{ns}"));
        Assert.Equal(PlanMountKind.ReadOnly, KindAt(plan, git + "/objects"));
        Assert.DoesNotContain(plan.Mounts, m => m.Kind == PlanMountKind.ReadWrite && m.Path.StartsWith(git + "/", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void Scratch_and_every_per_launch_directory_move_to_a_fresh_run_area()
    {
        var plan = Exec();

        var scratch = plan.Mounts.Last(m => m.Path == "/tmp");
        Assert.Equal(PlanMountKind.ReadWrite, scratch.Kind);
        Assert.Equal(ExecArea + "/tmp", scratch.Source);
        Assert.All(plan.Mounts.Where(m => m.Kind == PlanMountKind.PerLaunchDir), m => Assert.StartsWith(ExecArea + "/", m.Source, StringComparison.Ordinal));
        Assert.DoesNotContain(plan.Mounts, m => (m.Path + m.Source).Contains(Req.RunArea, StringComparison.Ordinal));
        Assert.Equal(ExecArea + "/sandbox-plan.json", plan.Environment.Single(e => e.Name == "AW_SANDBOX_PLAN").Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_socket_mount_binds_the_execs_own_socket_directory()
    {
        var stored = SandboxPlannerTests.Build(Req.WriteScope);

        var plan = Exec(stored);

        Assert.Equal(ExecNet, plan.Network.NetDir);
        Assert.Equal(ExecNet, plan.Mounts.Single(m => m.Path == "/run/sindri/net").Source);   // the stored launch's directory went with its egress
        Assert.DoesNotContain(plan.Mounts, m => string.Equals(m.Source, stored.Network.NetDir, StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void No_launcher_supplied_variable_and_no_session_home_survive()
    {
        var stored = SandboxPlannerTests.Build(Req.WriteScope);
        Assert.Contains(stored.Environment, e => e.Name == "AW_SINDRI_LAUNCH_SECRET" && e.Value is null);

        var plan = Exec(stored);

        Assert.DoesNotContain(plan.Environment, e => e.Value is null);
        Assert.DoesNotContain(plan.Environment, e => _launcherNames.Contains(e.Name, StringComparer.Ordinal));
        Assert.DoesNotContain(plan.Mounts, m => m.Path.Contains("-sessions/" + Req.WriteScope.Launch.Value, StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void It_has_its_own_launch_socket_directory_no_github_and_no_wall()
    {
        var plan = Exec();

        Assert.Equal(_exec, plan.Launch);
        Assert.Equal(ExecNet, plan.Network.NetDir);
        Assert.Equal(GithubCredentials.None, plan.Github);
        Assert.True(plan.Attended);
        Assert.Equal(Req.WriteScope.RunNamespace, plan.RunNamespace);   // what the agent saw is kept
    }

    [Fact, Trait("Lane", "unit")]
    public void A_plan_with_no_run_area_is_refused_in_one_line()
    {
        var stored = SandboxPlannerTests.Build(Req.WriteScope);
        var bare = stored with { Environment = [.. stored.Environment.Where(e => e.Name != "AW_SANDBOX_PLAN")] };

        var result = bare.ForExec(_exec, ExecArea, ExecNet);

        Assert.True(result.IsError);
        Assert.Equal(SindriErrors.InvalidInput.Code, result.Error.Code);
    }

    private static SandboxPlan Planned(SandboxMechanism mechanism)
    {
        var result = SandboxPlanner.Build(mechanism, Req.WriteScope);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        return result.Value;
    }

    [Fact, Trait("Lane", "unit")]
    public void A_seatbelt_exec_gets_its_own_short_temp_directory_and_never_the_live_launchs()
    {
        var stored = Planned(SandboxMechanism.Seatbelt);
        var live = SandboxPaths.SeatbeltTemp(stored.Launch);
        var own = SandboxPaths.SeatbeltTemp(_exec);
        Assert.NotEqual(live, own, StringComparer.Ordinal);

        var plan = Exec(stored);

        Assert.Contains(plan.Mounts, m => m.Kind == PlanMountKind.PerLaunchDir && m.Path == own);
        Assert.DoesNotContain(plan.Mounts, m => (m.Path + m.Source).Contains(live, StringComparison.Ordinal));
        Assert.Equal(own, plan.Environment.Single(e => e.Name == "TMPDIR").Value);
        Assert.Equal(own + "/dotnet-home", plan.Environment.Single(e => e.Name == "DOTNET_CLI_HOME").Value);
        Assert.DoesNotContain(plan.Environment, e => e.Value is { } v && v.Contains(live, StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_bubblewrap_exec_keeps_slash_tmp()
    {
        var plan = Exec(Planned(SandboxMechanism.Bubblewrap));

        Assert.Equal("/tmp", plan.Environment.Single(e => e.Name == "TMPDIR").Value);
        Assert.Equal(ExecArea + "/tmp", plan.Mounts.Last(m => m.Path == "/tmp").Source);
        Assert.DoesNotContain(plan.Mounts, m => m.Path.StartsWith("/private/tmp/aw-", StringComparison.Ordinal));
    }
}
