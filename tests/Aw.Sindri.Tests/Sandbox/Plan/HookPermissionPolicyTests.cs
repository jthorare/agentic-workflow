using Aw.Sindri.Core.Sandbox.Plan;
using Req = Aw.Sindri.Testing.Sandbox.SandboxRequests;

namespace Aw.Sindri.Tests.Sandbox.Plan;

/// <summary>The owner's answer about hooks (2026-10-18): keep is the default, only a hook that weakens the sandbox is treated. One case per inventory row.</summary>
public sealed class HookPermissionPolicyTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("block-destructive", HookTreatment.Keep)]
    [InlineData("block-push-main", HookTreatment.Keep)]
    [InlineData("context-guard", HookTreatment.Keep)]
    [InlineData("detect-secrets", HookTreatment.Keep)]
    [InlineData("done-gate", HookTreatment.Keep)]
    [InlineData("done-gate-annotate", HookTreatment.Keep)]
    [InlineData("external-write-guard", HookTreatment.Drop)]
    [InlineData("git-context", HookTreatment.Keep)]
    [InlineData("judge-health", HookTreatment.Keep)]
    [InlineData("meta-lane-gate", HookTreatment.Keep)]
    [InlineData("meta-outbox", HookTreatment.Keep)]
    [InlineData("meta-stop-gate", HookTreatment.Keep)]
    [InlineData("outbox-flush", HookTreatment.Keep)]
    [InlineData("prism-context", HookTreatment.Keep)]
    [InlineData("probe-log", HookTreatment.Keep)]
    [InlineData("prompt-sort", HookTreatment.Keep)]
    [InlineData("record-teammate-name", HookTreatment.Drop)]
    [InlineData("rtk-rewrite", HookTreatment.KeepDenyRewrites)]
    [InlineData("scope-gate", HookTreatment.Drop)]
    [InlineData("send-gate", HookTreatment.Drop)]
    [InlineData("session-id-report", HookTreatment.Keep)]
    [InlineData("sindri-nudge", HookTreatment.Keep)]
    [InlineData("subagent-start-map", HookTreatment.Keep)]
    [InlineData("turn-origin", HookTreatment.Keep)]
    [InlineData("winch", HookTreatment.Keep)]
    [InlineData("aw-live", HookTreatment.Keep)]
    public void Each_inventory_row_gets_its_treatment_in_the_session(string hook, HookTreatment treatment)
    {
        var hooks = HookPermissionPolicy.ForSession();

        Assert.Equal(treatment, HookPermissionPolicy.TreatmentOf(hook));
        Assert.Equal(treatment != HookTreatment.Drop, hooks.Kept.Contains(hook, StringComparer.Ordinal));
        Assert.Equal(treatment == HookTreatment.Drop, hooks.Omitted.Contains(hook, StringComparer.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_theory_covers_every_inventory_row()
    {
        var covered = typeof(HookPermissionPolicyTests).GetMethod(nameof(Each_inventory_row_gets_its_treatment_in_the_session))!
            .GetCustomAttributes(typeof(InlineDataAttribute), inherit: false).Cast<InlineDataAttribute>().Select(a => (string)a.Data[0]!).Order(StringComparer.Ordinal).ToList();
        var rows = HookInventory.Scripts.Select(s => s.Name).Order(StringComparer.Ordinal).ToList();

        Assert.Equal(rows, covered);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_hook_missing_from_the_inventory_is_kept_and_noted_in_the_plan()
    {
        var hooks = HookPermissionPolicy.ForSession(["block-destructive", "my-new-hook", "send-gate"]);

        Assert.Equal(HookTreatment.Keep, HookPermissionPolicy.TreatmentOf("my-new-hook"));
        Assert.Equal(["block-destructive", "my-new-hook"], hooks.Kept);
        Assert.Equal(["send-gate"], hooks.Omitted);
        var note = Assert.Single(hooks.Notes);
        Assert.Contains("my-new-hook", note, StringComparison.Ordinal);
        Assert.Contains("kept", note, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Only_the_installed_hooks_are_listed_and_the_inventory_hooks_give_no_note()
    {
        var hooks = HookPermissionPolicy.ForSession(["rtk-rewrite", "done-gate"]);

        Assert.Equal(["done-gate", "rtk-rewrite"], hooks.Kept);
        Assert.Empty(hooks.Omitted);
        Assert.Empty(hooks.Notes);
        Assert.Empty(HookPermissionPolicy.ForSession().Notes);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_plan_carries_the_notes_and_show_prints_them()
    {
        var request = Req.WriteScope with { Host = Req.Host with { InstalledHooks = ["done-gate", "my-new-hook"] } };

        var plan = SandboxPlannerTests.Build(request);

        Assert.Equal(["done-gate", "my-new-hook"], plan.Hooks.Kept);
        Assert.Contains("Note: " + Assert.Single(plan.Hooks.Notes), SandboxPlanRenderer.Show(plan), StringComparison.Ordinal);
    }
}
