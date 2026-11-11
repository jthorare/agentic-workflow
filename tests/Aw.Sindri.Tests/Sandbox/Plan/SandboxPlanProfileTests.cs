using Aw.Sindri.Core.Sandbox.Plan;
using Req = Aw.Sindri.Testing.Sandbox.SandboxRequests;

namespace Aw.Sindri.Tests.Sandbox.Plan;

/// <summary>#2866: the live profile's folder is bound read-only so the CLI inside a launch reads the profile in process, and AW_SINDRI_PROFILE points at it.</summary>
public sealed class SandboxPlanProfileTests
{
    private const string DefaultFolder = Req.SindriHome + "/profile";

    private static SandboxPlan Build(string? profilePath) =>
        SandboxPlannerTests.Build(Req.WriteScope with { Host = Req.Host with { ProfilePath = profilePath } });

    private static int At(SandboxPlan plan, string path, PlanMountKind kind) =>
        plan.Mounts.ToList().FindLastIndex(m => string.Equals(m.Path, path, StringComparison.Ordinal) && m.Kind == kind);

    private static string? Profile(SandboxPlan plan) => plan.Environment.SingleOrDefault(e => e.Name == "AW_SINDRI_PROFILE")?.Value;

    [Fact, Trait("Lane", "unit")]
    public void The_default_profile_folder_is_bound_read_only_after_the_masks_and_named_in_the_environment()
    {
        var plan = Build(DefaultFolder + "/profile.yaml");

        var bind = At(plan, DefaultFolder, PlanMountKind.ReadOnly);
        Assert.True(bind > At(plan, Req.SindriHome, PlanMountKind.Tmpfs));
        Assert.True(bind > At(plan, Req.SindriHome + "-sessions", PlanMountKind.Tmpfs));
        Assert.True(bind > At(plan, Req.SindriHome + "/launches", PlanMountKind.ReadWrite));
        Assert.Null(plan.Mounts[bind].Source);
        Assert.Equal(DefaultFolder + "/profile.yaml", Profile(plan));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_custom_profile_folder_is_bound()
    {
        var plan = Build("/srv/profiles/live.yaml");

        Assert.True(At(plan, "/srv/profiles", PlanMountKind.ReadOnly) >= 0);
        Assert.Equal("/srv/profiles/live.yaml", Profile(plan));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(Req.Home + "/profile.yaml", Req.Home)]
    [InlineData(Req.StateDir + "/profile.yaml", Req.StateDir)]
    [InlineData("/home/profile.yaml", "/home")]
    [InlineData(Req.SindriHome + "/profile.yaml", Req.SindriHome)]
    public void A_profile_folder_that_is_or_holds_a_masked_home_gets_no_bind_and_no_variable(string path, string folder)
    {
        var plan = Build(path);

        Assert.DoesNotContain(plan.Mounts, m => string.Equals(m.Path, folder, StringComparison.Ordinal) && m.Kind == PlanMountKind.ReadOnly);
        Assert.Null(Profile(plan));
    }

    [Fact, Trait("Lane", "unit")]
    public void No_profile_path_means_no_bind_and_no_variable()
    {
        var plan = Build(profilePath: null);

        Assert.DoesNotContain(plan.Mounts, m => m.Path.EndsWith("/profile", StringComparison.Ordinal));
        Assert.Null(Profile(plan));
    }
}
