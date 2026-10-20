using System.Text.Json;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Validation;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Wiring;

/// <summary>
/// Long-form recipes for the wiring and block rules, and a run of one rule group alone. The invalid fixtures under
/// Workflow/Recipes/invalid mirror these recipes; a test gives the positions the reader records for the nodes it reports.
/// </summary>
internal static class WiringRecipe
{
    public static RecipeState Active(string step, string? loop = null, IReadOnlyDictionary<string, WithValue>? with = null, IReadOnlyList<BlockUse>? inputs = null) => new(
        StateKind.Active, new StepUse(BlockId.From(step), with ?? NoArguments(), inputs ?? []), WaitFor: null,
        new TimeoutSpec(RecipeDuration.From("4h"), TimeoutAction.Park), loop, Budget: null, "Run the Step.", Outcome: null);

    public static RecipeState WaitForEvent(string kind) => Wait(new WaitFor(EventKind.From(kind), Answer: null, Resource: null, Child: null));

    public static RecipeState WaitForAnswer(string id) =>
        Wait(new WaitFor(Event: null, new AnswerWait(AnswerId.From(id), "Go on?"), Resource: null, Child: null));

    public static RecipeState WaitForResource(ResourceKind resource) => Wait(new WaitFor(Event: null, Answer: null, resource, Child: null));

    public static RecipeState WaitForChild(string recipe, params (string Key, WithValue Value)[] inputs) => Wait(new WaitFor(
        Event: null, Answer: null, Resource: null,
        new ChildWait(RecipeId.From(recipe), inputs.ToDictionary(i => FactKey.From(i.Key), i => i.Value), OnParked.Propagate)));

    public static RecipeState Terminal(string outcome = "resolved") =>
        new(StateKind.Terminal, Step: null, WaitFor: null, Timeout: null, Loop: null, Budget: null, Next: null, OutcomeName.From(outcome));

    public static RecipeTransition Go(
        string from,
        string to,
        string on = "step.completed",
        WhenClause? when = null,
        string[]? produces = null) =>
        new([from], to, on, when, [], [], [], [.. (produces ?? []).Select(p => new ProducedFact(FactKey.From(p), From: null))]);

    /// <summary>A transition whose 'produces' holds long-form entries.</summary>
    public static RecipeTransition GoProducing(string from, string to, string on, WhenClause? when, params ProducedFact[] produces) =>
        new([from], to, on, when, [], [], [], produces);

    /// <summary>A bare 'produces' entry.</summary>
    public static ProducedFact Bare(string fact) => new(FactKey.From(fact), From: null);

    /// <summary>A renamed 'produces' entry: <paramref name="fact"/> takes the value of <paramref name="from"/>.</summary>
    public static ProducedFact Rename(string fact, string from) => new(FactKey.From(fact), FactKey.From(from));

    /// <summary>An output declaration produced in <paramref name="from"/>.</summary>
    public static RecipeOutput OutputOf(string key, string from) => new(FactKey.From(key), Label: null, Type: null, from);

    public static WhenClause Outcome(string outcome) => ARecipe.When(OutcomeName.From(outcome));

    public static WhenClause BelowCap(string loop) => new(Outcome: null, Fact: null, Is: null, loop, LoopBound.Cap, All: null, Any: null, Not: null);

    public static WhenClause AllOf(params WhenClause[] clauses) => new(Outcome: null, Fact: null, Is: null, Loop: null, Below: null, clauses, Any: null, Not: null);

    public static WhenClause AnyOf(params WhenClause[] clauses) => new(Outcome: null, Fact: null, Is: null, Loop: null, Below: null, All: null, clauses, Not: null);

    public static GateUse Gate(string id, double? threshold = null, IReadOnlyDictionary<string, WithValue>? with = null) =>
        new(BlockId.From(id), threshold, with ?? NoArguments());

    public static BlockUse Use(string id, IReadOnlyDictionary<string, WithValue>? with = null) => new(BlockId.From(id), with ?? NoArguments());

    public static WithValue Literal(string json)
    {
        using var document = JsonDocument.Parse(json);
        return new WithValue(document.RootElement.Clone(), Fact: null, Profile: null);
    }

    public static WithValue FactValue(string key) => new(Literal: null, FactKey.From(key), Profile: null);

    public static Dictionary<string, WithValue> With(params (string Name, WithValue Value)[] arguments) =>
        arguments.ToDictionary(a => a.Name, a => a.Value, StringComparer.Ordinal);

    /// <summary>The input a recipe built without 'inputs' declares (RCP-INPUT-002 needs one).</summary>
    public const string DefaultInput = "fact:subject";

    public static Recipe Of(
        string initial,
        IEnumerable<(string Name, RecipeState State)> states,
        IEnumerable<RecipeTransition> transitions,
        string[]? inputs = null,
        string id = "workflow.wiring",
        IEnumerable<string>? loops = null) =>
        ARecipe.Minimal(RecipeId.From(id)) with
        {
            Inputs = [.. (inputs ?? [DefaultInput]).Select(k => new RecipeInput(FactKey.From(k), Label: null, Type: null, Kind: "text", Generate: null))],
            Initial = initial,
            States = states.ToDictionary(s => s.Name, s => s.State, StringComparer.Ordinal),
            Transitions = [.. transitions],
            Loops = (loops ?? []).ToDictionary(l => l, _ => new LoopSpec(3, LoopSpec.DefaultNoProgress), StringComparer.Ordinal),
        };

    /// <summary>The findings of the wiring rules for <paramref name="recipe"/>.</summary>
    public static IReadOnlyList<RecipeFinding> CheckWiring(Recipe recipe, Located? located = null) => Run(new WiringRules(), recipe, located ?? Located.Nowhere);

    /// <summary>The findings of the declaration rules for <paramref name="recipe"/>.</summary>
    public static IReadOnlyList<RecipeFinding> CheckDeclarations(Recipe recipe, Located? located = null) => Run(new DeclarationRules(), recipe, located ?? Located.Nowhere);

    /// <summary>The findings of the block rules for <paramref name="recipe"/>.</summary>
    public static IReadOnlyList<RecipeFinding> CheckBlocks(Recipe recipe, Located? located = null) => Run(new BlockRules(), recipe, located ?? Located.Nowhere);

    /// <summary>The findings of the human-trigger rule for <paramref name="recipe"/>.</summary>
    public static IReadOnlyList<RecipeFinding> CheckHumanTrigger(Recipe recipe, Located? located = null) => Run(new HumanTriggerRules(), recipe, located ?? Located.Nowhere);

    public static IReadOnlyList<string> Lines(this IEnumerable<RecipeFinding> findings) => [.. findings.Select(f => f.ToLine())];

    public static ChildRecipe Child(Recipe recipe) => new(recipe, RecipeHash.Of("{}"u8));

    private static IReadOnlyList<RecipeFinding> Run(IRecipeRules rules, Recipe recipe, Located located)
    {
        var source = new RecipeSource(located.File, new SourcePositions(
            located.Positions.ToDictionary(p => p.Path, p => new SourcePosition(p.Line, p.Column), StringComparer.Ordinal)));
        var document = new RecipeDocument(recipe, RecipeHash.Of("{}"u8), source);
        var context = new ValidationContext(located.Catalog ?? WiringCatalog.WithTestBlocks, located.Profile ?? RecipeProfileFacts.None,
            located.Children ?? InMemoryChildRecipes.None, BaseRecipe: null);
        return [.. rules.Check(document, context)];
    }

    private static RecipeState Wait(WaitFor waitFor) => new(
        StateKind.Wait, Step: null, waitFor, new TimeoutSpec(RecipeDuration.From("1d"), TimeoutAction.Park), Loop: null, Budget: null,
        "Waiting.", Outcome: null);

    private static Dictionary<string, WithValue> NoArguments() => new(StringComparer.Ordinal);
}
