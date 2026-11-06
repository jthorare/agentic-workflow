using Aw.Sindri.Core.Sandbox.Plan;
using Req = Aw.Sindri.Testing.Sandbox.SandboxRequests;

namespace Aw.Sindri.Tests.Sandbox.Plan;

/// <summary>A skill launch's AW_DIR bind (skill flows spec §3.3): read-write, and never on top of a mask or a deny that sits inside it.</summary>
public sealed class SandboxPlanSkillStateTests
{
    private const string AwDir = Req.StateDir + "/web";

    [Fact, Trait("Lane", "unit")]
    public void The_skill_folders_are_read_write_through_the_state_dir_deny()
    {
        var plan = SandboxPlannerTests.Build(Skill(Req.WriteScope));

        Assert.Equal(PlanMountKind.ReadWrite, KindAt(plan, AwDir + "/reviews/42.json"));
        Assert.Equal(PlanMountKind.ReadWrite, KindAt(plan, Req.StateDir + "/bugfix/web/state.json"));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_sindri_home_inside_the_skill_folder_stays_hidden()
    {
        // why: F-1's hand test ran Sindri from a home under AW_DIR; the bind, added after the masks, brought the home back and the self-check refused the launch (SND-LAUNCH-016)
        const string home = AwDir + "/proof/home";
        var paths = Req.WriteScope.Paths with { RunDirectory = home + "/workflow/runs/x/s/1", RunArea = home + "/workflow/areas/run-1" };
        var plan = SandboxPlannerTests.Build(Skill(Req.WriteScope with { Host = Req.Host with { SindriHome = home }, Paths = paths }));

        Assert.Equal(PlanMountKind.ReadWrite, KindAt(plan, AwDir + "/reviews/42.json"));
        Assert.True(Hidden(KindAt(plan, home + "/auth/human-token")));
        Assert.True(Hidden(KindAt(plan, home + "-sessions/LCH-other/settings.json")));
        Assert.Equal(PlanMountKind.ReadWrite, KindAt(plan, home + "/launches/LCH-1"));
    }

    private static SandboxRequest Skill(SandboxRequest request) => request with
    {
        SkillEnvironment = new Dictionary<string, string>(StringComparer.Ordinal) { ["AW_DIR"] = AwDir + "/" },
    };

    private static bool Hidden(PlanMountKind? kind) => kind is PlanMountKind.Deny or PlanMountKind.Tmpfs;

    // The last mount at or above a path decides what the session sees there.
    private static PlanMountKind? KindAt(SandboxPlan plan, string path) =>
        plan.Mounts.LastOrDefault(m => string.Equals(m.Path, "/", StringComparison.Ordinal) || string.Equals(m.Path, path, StringComparison.Ordinal) || path.StartsWith(m.Path + "/", StringComparison.Ordinal))?.Kind;
}
