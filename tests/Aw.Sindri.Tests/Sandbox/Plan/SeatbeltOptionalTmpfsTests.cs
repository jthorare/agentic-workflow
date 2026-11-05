using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Ctx = Aw.Sindri.Core.Sandbox.Plan.PlanContext;
using Req = Aw.Sindri.Testing.Sandbox.SandboxRequests;

namespace Aw.Sindri.Tests.Sandbox.Plan;

/// <summary>Seatbelt has no tmpfs, so only the .NET runtime's scratch directory may turn into a write allowance on its real path (#2089); any other optional tmpfs must stay hidden like a required one.</summary>
public sealed class SeatbeltOptionalTmpfsTests
{
    private const string Corefx = Req.Home + "/.dotnet/corefx";
    private const string Secrets = Req.Home + "/.secrets";

    /// <summary>Rewrites the Linux plan for Seatbelt with one extra tmpfs placed after the home tmpfs and before the denies, as the real stack would carry it.</summary>
    private static SandboxPlan Rewrite(bool optional)
    {
        var request = Req.WriteScope;
        var linux = SandboxPlanner.Build(SandboxMechanism.Bubblewrap, request);
        Assert.True(linux.IsSuccess, linux.IsError ? linux.Error.Message : string.Empty);
        var plan = linux.Value;
        var mounts = plan.Mounts.ToList();
        var at = mounts.FindIndex(m => m.Kind == PlanMountKind.Tmpfs && string.Equals(m.Path, Req.Home, StringComparison.Ordinal)) + 1;
        mounts.Insert(at, new PlanMount(Secrets, PlanMountKind.Tmpfs, Size: 1024, Reason: "hides the host's secrets", Optional: optional));
        return SeatbeltPlan.Rewrite(plan with { Mounts = mounts }, Ctx.From(request));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_optional_tmpfs_that_is_not_the_runtime_scratch_stays_hidden_on_Seatbelt()
    {
        var required = Rewrite(optional: false).Mounts.Last(m => m.Path == Secrets);
        var optional = Rewrite(optional: true).Mounts.Last(m => m.Path == Secrets);

        Assert.Equal(PlanMountKind.Deny, required.Kind);
        Assert.NotEqual(PlanMountKind.ReadWrite, optional.Kind);
        Assert.Equal(required.Kind, optional.Kind);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_runtime_scratch_still_gets_a_write_allowance_on_Seatbelt()
    {
        var plan = SandboxPlanner.Build(SandboxMechanism.Seatbelt, Req.WriteScope);
        Assert.True(plan.IsSuccess, plan.IsError ? plan.Error.Message : string.Empty);

        Assert.Equal(PlanMountKind.ReadWrite, plan.Value.Mounts.Last(m => m.Path == Corefx).Kind);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_only_optional_tmpfs_the_plan_makes_is_the_runtime_scratch()
    {
        var plan = SandboxPlanner.Build(SandboxMechanism.Bubblewrap, Req.WriteScope);
        Assert.True(plan.IsSuccess, plan.IsError ? plan.Error.Message : string.Empty);

        var optionalTmpfs = plan.Value.Mounts.Where(m => m.Kind == PlanMountKind.Tmpfs && m.Optional).ToList();

        Assert.NotEmpty(optionalTmpfs);
        Assert.All(optionalTmpfs, m =>
        {
            Assert.Equal(Corefx, m.Path);
            Assert.Equal(4L * 1024 * 1024, m.Size);
        });
    }
}
