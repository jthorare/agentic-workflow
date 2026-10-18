using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Req = Aw.Sindri.Testing.Sandbox.SandboxRequests;

namespace Aw.Sindri.Tests.Sandbox.Plan;

/// <summary>The product default sandbox (sandbox spec SB12): every call is SND-LAUNCH-014 until SB-5a.</summary>
public sealed class UnavailableSandboxTests
{
    private static void Refused(Error error)
    {
        Assert.Equal(SindriErrors.LaunchSandboxUnavailable.Code, error.Code);
        Assert.Equal("unsupported-platform", SandboxRefusal.ReasonOf(error));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task UnavailableSandbox_refuses_every_call_with_014()
    {
        var sandbox = new UnavailableSandbox();
        var plan = SandboxPlannerTests.Build(Req.WriteScope);

        Refused(sandbox.Probe().Error);
        Refused(sandbox.Plan(Req.WriteScope).Error);
        Refused(sandbox.Plan(Req.Attended).Error);
        Refused((await sandbox.SelfCheck(plan, TestContext.Current.CancellationToken)).Error);
        Refused(sandbox.Wrap(plan, ["claude"]).Error);
    }
}
