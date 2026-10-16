using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Host.Commands.Recipe;

namespace Aw.Sindri.Tests.ApproveRecipe;

/// <summary>What the approval screen says is true (PR 232 proof F1, F3): the effect allowlist note reads the approved profile, and the wake counts say what they count.</summary>
public sealed class ApprovalPreviewTruthTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory, Trait("Lane", "unit")]
    [InlineData("effect.claim.ref", true)]
    [InlineData("effect.notify.desk", false)]
    public async Task The_preview_marks_an_effect_allowlisted_only_when_the_approved_profile_lists_it(string listed, bool allowlisted)
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();
        var profile = new SindriProfile(SindriProfile.CurrentSchemaVersion, "tester", Workflow: new WorkflowSettings(Effects: [listed], DispatchDelay: null));
        _ = await r.H.Profiles.Save(new LoadedProfile(profile, ProfileHash.From(new string('a', 64)), "/home/profile.yaml"), Token);
        _ = await r.Approve(await r.Shipped("sindri/workflow.step.yaml"));

        var approved = (await r.Approve(await r.Shipped("sindri/workflow.item.yaml"))).Value;

        var effect = Assert.Single(approved.Preview.Effects);
        Assert.Equal(allowlisted, effect.Allowlisted);
        Assert.Contains(allowlisted ? "effect.claim.ref (on the effect allowlist)" : "effect.claim.ref (not on the effect allowlist)", RecipePreviewText.Approve(approved.Preview, evals: null), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_wake_counts_are_worded_as_open_instances_woken_not_as_instances_resumed()
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();
        var step = await r.Shipped("sindri/workflow.step.yaml");
        _ = await r.Approve(step);
        r.Wake.Run(step.Hash, WorkflowInstanceId.For(new Guid(8, 0, 0, new byte[8])));
        r.Wake.Run(step.Hash, WorkflowInstanceId.For(new Guid(9, 0, 0, new byte[8])));

        var again = (await r.Approve(step)).Value;

        var text = RecipeDeskCommands.Approved(again);
        Assert.Contains("woke 2 open instances", text, StringComparison.Ordinal);
        Assert.DoesNotContain("instances resumed", text, StringComparison.Ordinal);
        var preview = RecipePreviewText.Approve(again.Preview, evals: null);
        Assert.DoesNotContain("resumes:", preview, StringComparison.Ordinal);
        Assert.Contains("open instances woken (those parked on this drift resume):", preview, StringComparison.Ordinal);
    }
}
