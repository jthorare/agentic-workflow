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

    [Fact, Trait("Lane", "unit")]
    public void A_trial_skills_folder_under_home_is_mounted_read_only_after_the_home_tmpfs()
    {
        const string trial = Req.Home + "/.agentic-workflow/trial/skills";
        var plan = SandboxPlannerTests.Build(Skill(Req.WriteScope) with { SkillTrialDirectory = trial });

        var mount = Assert.Single(plan.Mounts, m => string.Equals(m.Path, trial, StringComparison.Ordinal));
        Assert.Equal(PlanMountKind.ReadOnly, mount.Kind);
        Assert.True(plan.Mounts.ToList().IndexOf(mount) > plan.Mounts.ToList().FindIndex(m => string.Equals(m.Path, Req.Home, StringComparison.Ordinal)));
        Assert.Equal(PlanMountKind.ReadOnly, KindAt(plan, trial + "/review/SKILL.md"));
    }

    [Fact, Trait("Lane", "unit")]
    public void No_trial_folder_means_no_extra_mount()
    {
        var plan = SandboxPlannerTests.Build(Skill(Req.WriteScope));

        Assert.DoesNotContain(plan.Mounts, m => m.Path.Contains("/trial/", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_write_under_the_skill_folder_is_allowed_and_not_read_denied()
    {
        var plan = SandboxPlannerTests.Build(Listed(Skill(Req.WriteScope)));

        Assert.Contains(plan.Policy.Rules, r => r.Effect == InnerRuleEffect.Allow && r.Tool == "Write" && r.Pattern == AwDir + "/**");
        Assert.False(ReadDenied(plan, AwDir + "/reviews/2148.json"));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_path_elsewhere_under_the_denied_state_dir_stays_read_denied()
    {
        var plan = SandboxPlannerTests.Build(Listed(Skill(Req.WriteScope)));

        Assert.True(ReadDenied(plan, Req.StateDir + "/locks/heavy"));
        Assert.True(ReadDenied(plan, Req.StateDir + "/other-repo/reviews/1.json"));
        Assert.DoesNotContain(plan.Policy.Rules, r => r.Effect == InnerRuleEffect.Allow && r.Tool == "Write" && r.Pattern == Req.StateDir + "/**");
    }

    private static SandboxRequest Listed(SandboxRequest request)
    {
        Dictionary<string, string[]> tree = new(StringComparer.Ordinal) { [Req.StateDir] = ["web", "other-repo", "locks", "sindri-app"] };
        return request with { Host = request.Host with { DirectoryEntries = dir => tree.GetValueOrDefault(dir) ?? [] } };
    }

    private static bool ReadDenied(SandboxPlan plan, string path) =>
        plan.Policy.Rules.Any(r => r.Effect == InnerRuleEffect.Deny && r.Tool == "Read" && r.Pattern.EndsWith("/**", StringComparison.Ordinal)
            && (string.Equals(path, r.Pattern[..^3], StringComparison.Ordinal) || path.StartsWith(r.Pattern[..^2], StringComparison.Ordinal)));

    private static SandboxRequest Skill(SandboxRequest request) => request with
    {
        SkillEnvironment = new Dictionary<string, string>(StringComparer.Ordinal) { ["AW_DIR"] = AwDir + "/" },
    };

    private static bool Hidden(PlanMountKind? kind) => kind is PlanMountKind.Deny or PlanMountKind.Tmpfs;

    // The last mount at or above a path decides what the session sees there.
    private static PlanMountKind? KindAt(SandboxPlan plan, string path) =>
        plan.Mounts.LastOrDefault(m => string.Equals(m.Path, "/", StringComparison.Ordinal) || string.Equals(m.Path, path, StringComparison.Ordinal) || path.StartsWith(m.Path + "/", StringComparison.Ordinal))?.Kind;
}
