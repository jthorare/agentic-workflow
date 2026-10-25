using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.ApproveAll;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Core.Workflow.Shipped;
using Aw.Sindri.Features.ApproveAllRecipes;
using Aw.Sindri.Features.ApproveRecipe;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Testing.Workflow.ApproveAll;
using Aw.Sindri.Tests.ApproveAll;
using Aw.Sindri.Tests.Workflow;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.ApproveRecipe;

/// <summary>
/// A shipped version that gains a capability waits for its own typed approval and is not registered yet (#983): the recipe list names it with its hash,
/// and 'approve id@hash' and 'approve id version' work on it, so the needs-you row's CLI line and the app's typed box both reach an approval.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class ApproveWaitingShippedTests
{
    private const string File = "workflow.step.yaml";
    private static readonly RecipeId _step = RecipeId.From("workflow.step");
    private static readonly string _yaml = SeededRecipeYaml.Of("workflow.step", 2, "a", "One step", [], withEvals: true);

    private static RecipeHash ShippedHash => RecipeReaderTests.Reader.Read(_yaml, File).Document!.Hash;

    private static RegistryHarness Harness()
    {
        var fake = new FakeShippedRecipes([]);
        fake.OwnApproval.Add(new OwnApprovalRow(_step, "One step", OwnApprovalReason.GainsCapability, "One step can now do more.", []));
        return new RegistryHarness(services => services
            .AddSingleton<IEmbeddedRecipes>(FakeEmbeddedRecipes.Of((File, _yaml)))
            .AddShippedRecipes()
            .AddSingleton<IShippedRecipes>(fake));
    }

    [Fact]
    public async Task The_list_names_the_waiting_shipped_version_with_its_hash_though_it_is_not_registered()
    {
        await using var registry = Harness();

        var line = Assert.Single(await registry.List(), l => l.Hash == ShippedHash);

        Assert.Equal((_step, 2, RecipeStanding.Unapproved, true, "changed"), (line.Id, line.Version, line.Standing, line.GainsCapability, line.Status));
    }

    [Fact]
    public async Task The_stale_list_names_the_waiting_shipped_version_too()
    {
        await using var registry = Harness();

        _ = Assert.Single(await registry.List(stale: true), l => l.Hash == ShippedHash);
    }

    [Fact]
    public async Task Approve_by_id_and_hash_registers_the_shipped_version_and_approves_it()
    {
        await using var registry = Harness();
        _ = await registry.H.HoldLease();

        var approved = await registry.Run<ApproveRecipeRequest, ApproveRecipeResponse>(new ApproveRecipeRequest(_step, ShippedHash), RegistryHarness.Human);

        Assert.True(approved.IsSuccess, approved.IsError ? approved.Error.ToString() : null);
        Assert.Equal((_step, 2, ShippedHash), (approved.Value.Approval.Id, approved.Value.Approval.Version, approved.Value.Approval.Hash));
    }

    [Fact]
    public async Task The_challenge_for_id_and_hash_and_for_id_and_version_registers_the_shipped_version_first()
    {
        await using var registry = Harness();
        _ = await registry.H.HoldLease();   // registering records 'recipe.registered', which needs the ledger lease

        var byHash = await registry.Issue($"approve workflow.step@{ShippedHash.Value}");
        var byVersion = await registry.Issue("approve workflow.step 2");

        Assert.True(byHash.IsSuccess, byHash.IsError ? byHash.Error.ToString() : null);
        Assert.True(byVersion.IsSuccess, byVersion.IsError ? byVersion.Error.ToString() : null);
    }

    [Fact]
    public async Task A_hash_no_shipped_recipe_has_is_still_not_found()
    {
        await using var registry = Harness();
        _ = await registry.H.HoldLease();   // registering records 'recipe.registered', which needs the ledger lease

        var issued = await registry.Issue("approve workflow.step@sha256:" + new string('0', 64));

        Assert.True(issued.IsError);
        Assert.Equal(WorkflowErrors.RecipeNotFound.Code, issued.Error.Code);
    }
}
