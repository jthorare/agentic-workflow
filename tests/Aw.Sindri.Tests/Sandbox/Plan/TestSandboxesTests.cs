using Aw.Sindri.Core;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Testing.Sandbox;
using Req = Aw.Sindri.Testing.Sandbox.SandboxRequests;

namespace Aw.Sindri.Tests.Sandbox.Plan;

/// <summary>The test-only sandboxes (sandbox spec SB12).</summary>
public sealed class TestSandboxesTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task PassThroughSandbox_plans_for_real_wraps_nothing_and_passes_its_self_check()
    {
        var sandbox = new PassThroughSandbox();

        var plan = sandbox.Plan(Req.WriteScope).Value;
        var wrapped = sandbox.Wrap(plan, ["claude", "--x"]);
        var check = await sandbox.SelfCheck(plan, TestContext.Current.CancellationToken);

        Assert.Equal(SandboxMechanism.PassThrough, plan.Mechanism);
        Assert.Equal(SandboxMechanism.PassThrough, sandbox.Probe().Value.Mechanism);
        Assert.Equal(["claude", "--x"], wrapped.Value);
        Assert.True(check.IsSuccess);
        Assert.Equal(plan.PlanHash, plan.ToSummary().PlanHash);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task RecordingSandbox_records_each_call_and_fails_the_next_self_check_on_request()
    {
        var sandbox = new RecordingSandbox();
        var plan = sandbox.Plan(Req.ReadOnly).Value;
        _ = sandbox.Wrap(plan, ["codex"]);
        sandbox.FailSelfCheckWith(SandboxRefusal.For(SindriErrors.LaunchSelfCheckFailed, SandboxReasons.GitRef));

        var failed = await sandbox.SelfCheck(plan, TestContext.Current.CancellationToken);
        var passed = await sandbox.SelfCheck(plan, TestContext.Current.CancellationToken);

        Assert.Equal([Req.ReadOnly], sandbox.Requests);
        Assert.Equal(["codex"], Assert.Single(sandbox.Wrapped));
        Assert.Equal(2, sandbox.Checked.Count);
        Assert.Equal("git-ref", SandboxRefusal.ReasonOf(failed.Error));
        Assert.True(passed.IsSuccess);
    }
}
