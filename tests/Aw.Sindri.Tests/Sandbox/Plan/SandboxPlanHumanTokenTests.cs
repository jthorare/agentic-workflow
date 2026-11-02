using Aw.Sindri.Core.Sandbox.Plan;
using Req = Aw.Sindri.Testing.Sandbox.SandboxRequests;

namespace Aw.Sindri.Tests.Sandbox.Plan;

/// <summary>The human token lives at &lt;real Sindri home&gt;/auth/human.token: a Linux sandboxed launch must never be handed that folder, a path to it or a variable that names it (#1099).</summary>
public sealed class SandboxPlanHumanTokenTests
{
    private const string Auth = Req.SindriHome + "/auth";

    private static bool UnderAuth(string? path) =>
        path is not null && (string.Equals(path, Auth, StringComparison.Ordinal) || path.StartsWith(Auth + "/", StringComparison.Ordinal));

    [Fact, Trait("Lane", "unit")]
    public void A_sandboxed_launch_never_mounts_the_real_home_auth_folder()
    {
        var plan = SandboxPlannerTests.Build(Req.WriteScope);

        Assert.Contains(plan.Mounts, m => m.Kind == PlanMountKind.Tmpfs && string.Equals(m.Path, Req.SindriHome, StringComparison.Ordinal));
        Assert.DoesNotContain(plan.Mounts, m => m.Kind is PlanMountKind.ReadOnly or PlanMountKind.ReadWrite && string.Equals(m.Path, Req.SindriHome, StringComparison.Ordinal));
        Assert.All(plan.Mounts, m =>
        {
            Assert.False(UnderAuth(m.Path), $"{m.Kind} at {m.Path}");
            Assert.False(UnderAuth(m.Source), $"{m.Kind} of {m.Source}");
        });
    }

    [Fact, Trait("Lane", "unit")]
    public void A_sandboxed_launch_environment_points_at_the_session_home_not_the_real_one()
    {
        var plan = SandboxPlannerTests.Build(Req.WriteScope);

        var home = plan.Environment.Single(e => e.Name == "AW_SINDRI_HOME").Value;
        Assert.NotNull(home);
        Assert.NotEqual(Req.SindriHome, home, StringComparer.Ordinal);
        Assert.All(plan.Environment, e => Assert.False(UnderAuth(e.Value), $"{e.Name}={e.Value}"));
    }
}
