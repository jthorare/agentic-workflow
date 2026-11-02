using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.ApproveAll;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Core.Workflow.Shipped;
using Aw.Sindri.Core.Workflow.Validation;
using Aw.Sindri.Features.ApproveAllRecipes;
using Aw.Sindri.Features.ListRecipes;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Testing.Workflow.ApproveAll;
using Aw.Sindri.Tests.ApproveAll;
using Aw.Sindri.Tests.Workflow;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.ApproveRecipe;

/// <summary>
/// The bug check for issue 1042: a shipped version nobody has approved is visible before anything is approved. This build ships two newer versions of recipes that are registered and approved at
/// their v1: workflow.step v2 (no new capability: approve-all takes it) and workflow.item v2 (gains a capability: it waits for its own 'approve id@hash'). The recipe list shows both and says which way each
/// is approved ('approveVia': 'approveAll' or 'own', a new member of a list line), 'recipe show id@hash' prints each from the embedded set without registering it, and approving a parent that uses
/// workflow.step names the child version it pins and warns that a newer shipped child waits.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class WaitingShippedVersionsCheckTests
{
    private static readonly RecipeId _step = RecipeId.From("workflow.step");
    private static readonly RecipeId _item = RecipeId.From("workflow.item");
    private static readonly RecipeId _line = RecipeId.From("workflow.line");

    private static string Yaml(string id, int version, string content, params string[] children) => SeededRecipeYaml.Of(id, version, content, id, children, withEvals: true);

    private static RecipeDocument Document(string yaml, string file) => RecipeReaderTests.Reader.Read(yaml, file).Document!;

    private sealed class Shipping
    {
        public Shipping()
        {
            var stepV2 = Yaml("workflow.step", 2, "b");
            var itemV2 = Yaml("workflow.item", 2, "b");
            StepV1 = Document(Yaml("workflow.step", 1, "a"), "workflow.step.yaml");
            ItemV1 = Document(Yaml("workflow.item", 1, "a"), "workflow.item.yaml");
            StepV2 = Document(stepV2, "workflow.step.yaml").Hash;
            ItemV2 = Document(itemV2, "workflow.item.yaml").Hash;
            Parent = Document(Yaml("workflow.line", 3, "p", "workflow.step"), "workflow.line.yaml");
            var fake = new FakeShippedRecipes([new ShippedSeed(_step, "Step", StepV2, [], [], StepV1.Hash, 2)]);
            fake.OwnApproval.Add(new OwnApprovalRow(_item, "Item", OwnApprovalReason.GainsCapability, "Item can now do more.", []) { Hash = ItemV2, Version = 2 });
            Registry = new RegistryHarness(services => services
                .AddSingleton<IEmbeddedRecipes>(FakeEmbeddedRecipes.Of(("workflow.step.yaml", stepV2), ("workflow.item.yaml", itemV2)))
                .AddShippedRecipes()
                .AddSingleton<IShippedRecipes>(fake));
        }

        public RegistryHarness Registry { get; }

        public RecipeDocument StepV1 { get; }

        public RecipeDocument ItemV1 { get; }

        public RecipeDocument Parent { get; }

        public RecipeHash StepV2 { get; }

        public RecipeHash ItemV2 { get; }

        /// <summary>The v1 of each recipe registered and approved, and the parent that uses workflow.step registered (not approved).</summary>
        public async Task<RegisteredRecipe> Seed()
        {
            await Registry.ApproveAsInThePast(await Registry.Register(StepV1));
            await Registry.ApproveAsInThePast(await Registry.Register(ItemV1));
            return await Registry.Register(Parent);
        }
    }

    private static string? ApproveVia(JsonElement lines, RecipeHash hash) =>
        lines.EnumerateArray().Where(l => l.GetProperty("hash").GetString() == hash.Value).Select(l => l.TryGetProperty("approveVia", out var via) ? via.GetString() : null).SingleOrDefault();

    [Fact(Timeout = 30_000)]
    public async Task The_list_shows_each_waiting_shipped_version_and_says_how_it_is_approved()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        var shipping = new Shipping();
        await using var registry = shipping.Registry;
        _ = await shipping.Seed();

        var listed = await registry.Run<ListRecipesRequest, ListRecipesResponse>(new ListRecipesRequest(Stale: false), RegistryHarness.Human);

        Assert.True(listed.IsSuccess, listed.IsError ? listed.Error.ToString() : null);
        using var json = JsonDocument.Parse(AwJson.Serialize(listed.Value, SliceHarness.Json));
        var lines = json.RootElement.GetProperty("recipes");
        Assert.Equal(("approveAll", "own"), (ApproveVia(lines, shipping.StepV2), ApproveVia(lines, shipping.ItemV2)));
    }

    [Fact(Timeout = 30_000)]
    public async Task Show_prints_each_waiting_shipped_version_before_anything_is_approved_and_registers_nothing()
    {
        var shipping = new Shipping();
        await using var registry = shipping.Registry;
        _ = await registry.Register(shipping.StepV1);
        _ = await registry.Register(shipping.ItemV1);

        var step = await registry.Show(_step, shipping.StepV2.Value);
        var item = await registry.Show(_item, shipping.ItemV2.Value);

        Assert.True(step.IsSuccess && item.IsSuccess, step.IsError ? step.Error.ToString() : item.IsError ? item.Error.ToString() : null);
        Assert.Equal((2, shipping.StepV2, 2, shipping.ItemV2), (step.Value.Version, step.Value.Hash, item.Value.Version, item.Value.Hash));
        var stored = await registry.Recipes.Versions(_step, TestContext.Current.CancellationToken);
        _ = Assert.Single(stored.Value);   // why: showing a waiting version reads the embedded set, it does not register it
    }

    [Fact(Timeout = 30_000)]
    public async Task Approving_a_parent_names_the_child_version_it_pins_and_warns_that_a_newer_shipped_child_waits()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        var shipping = new Shipping();
        await using var registry = shipping.Registry;
        var parent = await shipping.Seed();
        _ = await registry.H.HoldLease();

        var issued = await registry.Issue($"approve {_line.Value}@{parent.Hash.Value}");

        Assert.True(issued.IsSuccess, issued.IsError ? issued.Error.ToString() : null);
        var text = JsonSerializer.Serialize(JsonDocument.Parse(AwJson.Serialize(issued.Value, SliceHarness.Json)).RootElement);
        Assert.Contains(shipping.StepV1.Hash.Value, text, StringComparison.Ordinal);   // the child version the approval pins
        Assert.Contains(shipping.StepV2.Digest[..12], text, StringComparison.Ordinal);   // the newer shipped child that waits
        Assert.Contains("newer", text, StringComparison.OrdinalIgnoreCase);
    }
}
