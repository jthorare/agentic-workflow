using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow;
using Aw.Sindri.Tests.Workflow.Engine;

namespace Aw.Sindri.Tests.Layout;

/// <summary>The shipped dev recipes as the engine would hold them at their desk, built through InstanceViews.Of and Detail (what the contract and invariant tests run on).</summary>
internal static class DevInstances
{
    public static readonly DateTimeOffset Since = new(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset AsOf = new(2026, 10, 12, 12, 26, 0, TimeSpan.Zero);

    public static (InstanceView View, InstanceDetail Detail) Produce(string file, string state, string item, string title, string requestFact, string requestText, string inputName = "request.txt", (string Fact, string Text, string Name)? second = null)
    {
        var path = ShippedRecipes.Recipes.Single(p => p.EndsWith(file, StringComparison.Ordinal));
        var loaded = RecipeComposition.ReadClean(ShippedRecipes.Read(path), path);
        var key = FactKey.From(requestFact);
        var value = EngineFixture.Fact(requestText);
        var instance = EngineFixture.At(loaded.Recipe, state) with
        {
            Address = InstanceAddress.Root(ItemKey.From(item), loaded.Recipe.Id, 1),
            Item = ItemKey.From(item),
            Recipe = new PinnedRecipe(loaded.Recipe.Id, loaded.Recipe.Version, loaded.Hash, new Dictionary<RecipeId, RecipeHash>()),
            Facts = Facts(),
        };
        var recipe = new InterpretedRecipe(
            new RecipeVersion(loaded.Recipe.Id, loaded.Recipe.Version, loaded.Hash), loaded.Recipe, EngineFixture.Catalog, Inputs());
        var provenance = Inputs().ToDictionary(static i => i.Key, static _ => new FactProvenance("human:cli", Since));
        var name = InstanceNames.Render(recipe, ItemKey.From(item), new Dictionary<FactKey, string?> { [key] = requestText }, Since);
        var view = InstanceViews.Of(instance, recipe, Since, Since.AddDays(2), new InstanceViewContext(Version: 3, provenance, Subject: SubjectRefs.Of(recipe, ItemKey.From(item), requestText, name)));
        return (view, InstanceViews.Detail(view, new InstanceDetailContext(title, Parent: null, new ViewCounts(2, 0, 0, Context: null), AsOf, RecipeDisplayName: Testing.Views.RecipeShape.Named(view.Recipe.Id.Value))));

        Dictionary<FactKey, FactValue> Facts() => Inputs().ToDictionary(static i => i.Key, static i => i.Value.Value);

        Dictionary<FactKey, InputFact> Inputs() => second is { } more
            ? new() { [key] = new(inputName, value), [FactKey.From(more.Fact)] = new(more.Name, EngineFixture.Fact(more.Text)) }
            : new() { [key] = new(inputName, value) };
    }

    /// <summary>Both dev scenarios.</summary>
    public static IReadOnlyList<(InstanceView View, InstanceDetail Detail)> All { get; } =
    [
        Produce("desk-round-trip.yaml", "export", "ITM-01JL1", "Desk round trip", "fact:request", "Summarize the week"),
        Produce("triage-three-state.yaml", "desk", "ITM-01JL2", "Three-state triage", "fact:scope", "Crash on launch", "scope.txt", ("fact:item", "ITM-01JL2 Crash on launch", "item.json")),
    ];
}
